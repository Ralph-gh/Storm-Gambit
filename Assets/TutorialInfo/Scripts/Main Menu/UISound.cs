using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public class UISound : MonoBehaviour
{
    // Start is called before the first frame update
    public AudioClip clickSound;
    private AudioSource audioSource;
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    public void PlayClick()
    {
        audioSource.PlayOneShot(clickSound);
    }
}
