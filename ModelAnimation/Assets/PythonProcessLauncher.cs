using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class PythonProcessLauncher : MonoBehaviour
{
    private Process pythonProcess;

    void Start()
    {
        // Path to the Python interpreter (from virtual environment)
        string pythonPath = "/Users/torence/PycharmProjects/media/venv/bin/python";

        // Path to the Python script to be executed
        string pythonScriptPath = "/Users/torence/PycharmProjects/media/main.py";

        Debug.Log($"Python Executable Path: {pythonPath}");
        Debug.Log($"Python Script Path: {pythonScriptPath}");

        // Initialize the process to launch Python
        pythonProcess = new Process();
        pythonProcess.StartInfo.FileName = pythonPath;
        pythonProcess.StartInfo.Arguments = pythonScriptPath;
        pythonProcess.StartInfo.UseShellExecute = false;
        pythonProcess.StartInfo.CreateNoWindow = true;
        pythonProcess.StartInfo.RedirectStandardOutput = true;
        pythonProcess.StartInfo.RedirectStandardError = true;

        // Log standard output from the Python script
        pythonProcess.OutputDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
                Debug.Log($"Python Output: {args.Data}");
        };

        // Log any errors from the Python script
        pythonProcess.ErrorDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
                Debug.LogError($"Python Error: {args.Data}");
        };

        // Attempt to start the Python process
        try
        {
            pythonProcess.Start();
            pythonProcess.BeginOutputReadLine();
            pythonProcess.BeginErrorReadLine();
            Debug.Log("Python script started successfully.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to start Python script: {e.Message}");
        }
    }

    void OnApplicationQuit()
    {
        // Ensure the Python process is killed when Unity closes
        if (pythonProcess != null && !pythonProcess.HasExited)
        {
            pythonProcess.Kill();
            pythonProcess.Dispose();
            Debug.Log("Python script stopped.");
        }
    }
}
