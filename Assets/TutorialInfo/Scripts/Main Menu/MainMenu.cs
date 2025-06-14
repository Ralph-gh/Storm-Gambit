using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    // Start is called before the first frame update

    public AudioClip drawSound;
    
    public AudioSource audioSource;
    public AudioClip clickSound;

    public void PlayGame()
    {
        
        SceneManager.LoadScene("GameScene");
    }

    IEnumerator PlayAndLoad()
    {
        audioSource.PlayOneShot(clickSound);
        yield return new WaitForSeconds(clickSound.length);  // Wait for the sound to finish
        SceneManager.LoadScene("GameScene"); // replace with your actual scene name
    }

    // Update is called once per frame
    public void OpenSettings()
    {
        // to be updated later with settings
        Debug.Log("Settings menu opened");
    }

    public void QuitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
            #endif
            }
}
