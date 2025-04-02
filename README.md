# Real-time Pose Estimation Motion Capture for Animation

This project combines 3D pose estimation and inverse kinematics to create a low-cost, markerless motion capture system for animating humanoid virtual models in Unity. Using MediaPipe and OpenCV for pose estimation, the system extracts 3D joint coordinates and streams them to Unity via UDP. Unity receives this data to animate a humanoid model using inverse kinematics (IK) or direct bone manipulation.

---

## 🔧 Features

- Real-time full-body motion capture
- UDP communication between Python and Unity
- Unity integration using Full Body IK
- Animation of humanoid models using pose data
- Support for 3D visualization and digital interaction

---

## 🧪 Setup Instructions

### Python Requirements

Install all dependencies with:

```bash
pip install -r requirements.txt
```

###  Running from Unity (Webcam Input)
1. Open Unity Hub and launch the project located in the ModelAnimation/ folder.
2. Open the main scene (e.g., MainScene.unity).
3. In the Hierarchy, ensure the following:
   - Your model (e.g., UnityChan) is placed in the scene and correctly set up with FullBodyUnityChanBehaviour from SAFullBodyIK.
   - The PythonProcessLauncher script is attached to a GameObject in the scene.
4. In the Inspector, open the PythonProcessLauncher component and set the correct absolute path to the pose_estimation.py file under the scriptPath field.
5. Press the Play button in Unity.
6. Unity will automatically launch the Python script, activate the webcam, and begin receiving pose data via UDP to drive the model.

###   Running Manually (Terminal / Standalone Use)
Step 1: Run the Python Script (the script can be run with a presaved video/webcam input)
Open a terminal and run:
```bash
python pose_estimation.py "Optional Video Path"
```

Step 2: Launch Unity - Open the Unity project via Unity Hub and press Play
