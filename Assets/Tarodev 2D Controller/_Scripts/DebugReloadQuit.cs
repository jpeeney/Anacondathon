using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DebugReloadQuit : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown("escape")) Application.Quit();

        if (Input.GetKeyDown("r")) SceneManager.LoadSceneAsync(0);
    }
}
