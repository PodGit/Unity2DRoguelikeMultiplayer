using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace Completed
{
    public class Lobby : MonoBehaviour
    {
        public int playerPanelSpacing = 100;

        // Start is called before the first frame update
        void Start()
        {
            InitialisePanels();
        }

        // Update is called once per frame
        void Update()
        {

        }

        void InitialisePanels()
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

                    NetworkManager.Peer currentPeer = NetworkManager.Instance.GetPeer(playerIdx);

                    GameObject currentPanel = GameObject.Find("LobbyPlayerPanel" + (playerIdx + 1));
                    UnityEngine.UI.Text currentPanelNameText = currentPanel.transform.Find("LobbyPlayerNameText").GetComponent<UnityEngine.UI.Text>();
                    currentPanelNameText.text = "Name: " + currentPeer.GetName();
                }
            }
        }

        void SetPanelEnabled(int index, bool enabled)
        {
            GameObject playerPanel = GameObject.Find("LobbyPlayerPanel" + (index + 1));
            playerPanel.SetActive(enabled);
        }
    }
}
