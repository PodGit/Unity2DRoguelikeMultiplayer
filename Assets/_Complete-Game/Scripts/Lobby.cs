using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace Completed
{
    public class Lobby : MonoBehaviour
    {
        bool panelsNeedsRefreshed = false;

        public int playerPanelSpacing = 100;

        GameObject[] playerPanels;

        // Start is called before the first frame update
        void Start()
        {
            NetworkManager.Instance.RegisterPeerAddedCallback(this.OnPeerAdded);
            NetworkManager.Instance.RegisterPeerRemovedCallback(this.OnPeerRemoved);

            InitPanels();
            RefreshPanels();

            GameObject go = new GameObject("Debugger");
            go.AddComponent<Debugger>();

            GameObject startGameBtn = GameObject.Find("LobbyBtnStartGame");
            if (!NetworkManager.Instance.IsHost())
            {
                startGameBtn.SetActive(false);
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (panelsNeedsRefreshed)
            {
                RefreshPanels();
                panelsNeedsRefreshed = false;
            }

            if (NetworkManager.Instance.GetGameStarted())
            {
                StartGame();
            }
        }

        void InitPanels()
        {
            playerPanels = new GameObject[GameManager.MaxNumPlayers];

            for (int playerIdx = 0; playerIdx < GameManager.MaxNumPlayers; playerIdx++)
            {
                playerPanels[playerIdx] = GameObject.Find("LobbyPlayerPanel" + (playerIdx + 1));
            }
        }

        void RefreshPanels()
        {
            for (int playerIdx = 0; playerIdx < GameManager.MaxNumPlayers; playerIdx++)
            {
                if (playerIdx >= NetworkManager.Instance.GetNumPeers())
                {
                    SetPanelEnabled(playerIdx, false);
                }
                else
                {
                    SetPanelEnabled(playerIdx, true);                    
                }
            }            
        }

        void OnPeerAdded(NetworkPeer peer)
        {
            Debug.Log("Peer added");
            panelsNeedsRefreshed = true;
        }

        void OnPeerRemoved(NetworkPeer peer)
        {
            Debug.Log("Peer removed");
        }

        void SetPanelEnabled(int index, bool enabled)
        {
            Debug.Log("Lobby::SetPanelEnabled peer:" + index + " enabled:" + (enabled ? "true":"false"));
            GameObject playerPanel = playerPanels[index];

            playerPanel.SetActive(enabled);

            if (enabled)
            {
                NetworkPeer peer = NetworkManager.Instance.GetPeer(index);

                UnityEngine.UI.Text currentPanelNameText = playerPanel.transform.Find("LobbyPlayerNameText").GetComponent<UnityEngine.UI.Text>();
                currentPanelNameText.text = "Name: " + peer.GetName();
            }
        }

        public void BackToMainMenu()
        {
            NetworkManager.Instance.Reset();
            SceneManager.LoadScene("MainMenu");
        }

        public void StartGamePressed()
        {
            NetworkManager.Instance.StartGame();
            StartGame();
        }

        void StartGame()
        {
            SceneManager.LoadScene("GameScene");
        }
    }
}
