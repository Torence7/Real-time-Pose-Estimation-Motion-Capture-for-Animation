# --- Import necessary libraries ---
import cv2  # For video capture and image processing
import mediapipe as mp  # For human pose estimation
import socket  # For sending data over UDP
import sys  # For reading command-line arguments
import numpy as np  # For numerical operations
import matplotlib.pyplot as plt  # For 2D/3D plotting
import matplotlib.animation as animation  # For updating the plot in real-time
from mpl_toolkits.mplot3d import Axes3D  # For 3D plotting
import time  # For FPS calculation

# --- UDP Configuration for communication with Unity ---
UDP_IP = "127.0.0.1"  # IP address of the receiver (typically Unity)
UDP_PORT = 5000  # Port number for the UDP socket
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)  # Create a UDP socket

# --- Initialize MediaPipe Pose model ---
mp_pose = mp.solutions.pose
mp_drawing = mp.solutions.drawing_utils
pose = mp_pose.Pose(model_complexity=2, static_image_mode=False,
                    min_detection_confidence=0.5,
                    min_tracking_confidence=0.5)

# --- Define mapping of MediaPipe landmarks to a simplified 17-joint skeleton (OpenPose-like) ---
POSE_17_MAPPING = [
    (23, 24),     # 0 - Mid-hip (average of left and right hip)
    (24,),        # 1 - Left Hip
    (26,),        # 2 - Left Knee
    (28,),        # 3 - Left Ankle
    (23,),        # 4 - Right Hip
    (25,),        # 5 - Right Knee
    (27,),        # 6 - Right Ankle
    (11, 12, 23, 24),  # 7 - Torso center (average of shoulders and hips)
    (11, 12),     # 8 - Mid-shoulders
    (9, 10),      # 9 - Mid-eyes
    (0,),         # 10 - Nose
    (11,),        # 11 - Right Shoulder
    (13,),        # 12 - Right Elbow
    (15,),        # 13 - Right Wrist
    (12,),        # 14 - Left Shoulder
    (14,),        # 15 - Left Elbow
    (16,)         # 16 - Left Wrist
]

# --- Define connections (bones) between joints for 3D visualization ---
connections = [
    (0, 1), (1, 2), (2, 3),       # Left leg
    (0, 4), (4, 5), (5, 6),       # Right leg
    (0, 7), (7, 8), (8, 9), (9, 10),  # Spine to head
    (8, 11), (11, 12), (12, 13),  # Right arm
    (8, 14), (14, 15), (15, 16)   # Left arm
]

def run_pose_estimation(video_path=None):
    """
    Main function to run real-time pose estimation using MediaPipe.
    Optionally takes a video path. If none, defaults to webcam.
    Sends 3D joint data over UDP and visualizes in a 3D plot.
    """
    cap = cv2.VideoCapture(video_path if video_path else 0)  # Open video or webcam

    # Set up 3D plot
    fig = plt.figure()
    ax = fig.add_subplot(111, projection='3d')
    ax.set_xlim([-1, 1])
    ax.set_ylim([-1, 1])
    ax.set_zlim([-1, 1])
    ax.set_xlabel('X')
    ax.set_ylabel('Z')
    ax.set_zlabel('Y')
    ax.set_title('Real-Time 3D Skeleton')

    # Initialize 3D scatter points and line objects for skeleton
    skeleton_points = ax.scatter([], [], [], c='r', marker='o')
    skeleton_lines = [ax.plot([], [], [], 'b')[0] for _ in connections]

    # For calculating frames-per-second (FPS)
    prev_time = [time.time()]

    def update_plot(frame):
        # --- Capture and process each frame ---
        ret, img = cap.read()
        if not ret:
            return skeleton_points,

        img = cv2.flip(img, 1)  # Mirror the image for a more natural webcam display
        rgb_img = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)  # Convert to RGB
        results = pose.process(rgb_img)  # Run pose estimation

        landmark_data = []  # Will store (x, y, z) for UDP
        visual_landmark_data = []  # Will store transformed coordinates for 3D plot

        if results.pose_landmarks:
            for mapping in POSE_17_MAPPING:
                # Handle joints with 1 landmark
                if len(mapping) == 1:
                    idx = mapping[0]
                    if idx < len(results.pose_landmarks.landmark):
                        lm = results.pose_landmarks.landmark[idx]
                        landmark_data.extend([lm.x, lm.y, lm.z])
                        visual_landmark_data.append((lm.x, lm.z, -lm.y))  # Flip Y for display
                    else:
                        landmark_data.extend([0.0, 0.0, 0.0])
                        visual_landmark_data.append((0.0, 0.0, 0.0))

                # Handle joints with 2 landmarks (average)
                elif len(mapping) == 2:
                    idx1, idx2 = mapping
                    if idx1 < len(results.pose_landmarks.landmark) and idx2 < len(results.pose_landmarks.landmark):
                        lm1, lm2 = results.pose_landmarks.landmark[idx1], results.pose_landmarks.landmark[idx2]
                        x, y, z = (lm1.x + lm2.x) / 2, (lm1.y + lm2.y) / 2, (lm1.z + lm2.z) / 2
                        landmark_data.extend([x, y, z])
                        visual_landmark_data.append((x, z, -y))
                    else:
                        landmark_data.extend([0.0, 0.0, 0.0])
                        visual_landmark_data.append((0.0, 0.0, 0.0))

                # Handle joints with 4 landmarks (average)
                elif len(mapping) == 4:
                    if all(idx < len(results.pose_landmarks.landmark) for idx in mapping):
                        lms = [results.pose_landmarks.landmark[idx] for idx in mapping]
                        avg_x = sum(l.x for l in lms) / 4
                        avg_y = sum(l.y for l in lms) / 4
                        avg_z = sum(l.z for l in lms) / 4
                        landmark_data.extend([avg_x, avg_y, avg_z])
                        visual_landmark_data.append((avg_x, avg_z, -avg_y))
                    else:
                        landmark_data.extend([0.0, 0.0, 0.0])
                        visual_landmark_data.append((0.0, 0.0, 0.0))

            # --- Send pose data to Unity via UDP ---
            if len(landmark_data) == 51:  # 17 joints x 3 values
                sock.sendto(",".join(map(str, landmark_data)).encode(), (UDP_IP, UDP_PORT))

            # --- Update 3D visualization ---
            visual_landmark_data = np.array(visual_landmark_data)
            skeleton_points._offsets3d = (visual_landmark_data[:, 0],
                                          visual_landmark_data[:, 1],
                                          visual_landmark_data[:, 2])

            # Update line segments between joints
            for i, (start, end) in enumerate(connections):
                skeleton_lines[i].set_data([visual_landmark_data[start, 0], visual_landmark_data[end, 0]],
                                           [visual_landmark_data[start, 1], visual_landmark_data[end, 1]])
                skeleton_lines[i].set_3d_properties([visual_landmark_data[start, 2], visual_landmark_data[end, 2]])

            # --- Display FPS in console ---
            curr_time = time.time()
            fps = 1.0 / (curr_time - prev_time[0])
            print(f"FPS: {fps:.2f}")
            prev_time[0] = curr_time

            # --- Draw pose on webcam image ---
            mp_drawing.draw_landmarks(img, results.pose_landmarks, mp_pose.POSE_CONNECTIONS)

        # Show processed video frame
        img = cv2.resize(img, (400, 300))
        cv2.imshow('Pose Estimation', img)
        if cv2.waitKey(1) & 0xFF == ord('q'):
            cap.release()
            cv2.destroyAllWindows()
            plt.close()

        return skeleton_points,

    # Animate the 3D plot in real-time
    ani = animation.FuncAnimation(fig, update_plot, interval=10, blit=False)
    plt.show()


# --- Main Entry Point ---
if __name__ == "__main__":
    # Optionally provide a video file path, else defaults to webcam
    video_path = sys.argv[1] if len(sys.argv) > 1 else None
    run_pose_estimation(video_path)
