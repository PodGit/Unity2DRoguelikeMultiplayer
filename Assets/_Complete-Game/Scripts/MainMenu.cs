using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Net;

public class MainMenu : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void StartGameSolo()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    public void StartGameHost()
    {
        NetworkManager.Instance.Host();
    }

    public void StartGameJoin()
    {
        try
        {
            UnityEngine.UI.InputField inputField = GameObject.Find("MainMenuJoinIPInput").GetComponent<UnityEngine.UI.InputField>();
            string ipAddress = inputField.text;

            if (ipAddress.Length > 0)
            {
                NetworkManager.Instance.Join(ipAddress);
            }
            else
            {
                throw new System.ArgumentException("IpAddress field empty");
            }
        }
        catch (Exception e)
        {
            Debug.Log("Exception: " + e.ToString());
        }
    }
}
