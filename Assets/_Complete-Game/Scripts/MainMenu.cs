﻿using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Net;

namespace Completed
{
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
            SceneManager.LoadScene("GameScene");
        }

        public void StartGameHost()
        {
            NetworkManager.Instance.Host(GetName());
            SceneManager.LoadScene("lobby");
        }

        public void StartGameJoin()
        {
            try
            {
                UnityEngine.UI.InputField inputField = GameObject.Find("MainMenuJoinIPInput").GetComponent<UnityEngine.UI.InputField>();
                string ipAddress = inputField.text;

                if (ipAddress.Length > 0)
                {
                    NetworkManager.Instance.Join(ipAddress, GetName());
                    SceneManager.LoadScene("Lobby");
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

        string GetName()
        {
            UnityEngine.UI.Text playerNameText = GameObject.Find("MainMenuNameInputText").GetComponent<UnityEngine.UI.Text>();
            return playerNameText.text;
        }

        public void ExitGame()
        {
            Application.Quit();
        }
    }
}