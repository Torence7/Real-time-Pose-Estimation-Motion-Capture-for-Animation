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

### Unity Requirements
