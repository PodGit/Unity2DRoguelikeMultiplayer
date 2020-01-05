using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Completed
{
    public class Debugger : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        private void OnGUI()
        {
            int labelX = 100;
            int labelY = 100;
            int labelSpacing = 300;
            int labelWidth = 300;
            int labelHeight = 150;

            for (int peerIdx = 0; peerIdx < NetworkManager.Instance.GetNumPeers(); ++peerIdx)
            {
                NetworkPeer peer = NetworkManager.Instance.GetPeer(peerIdx);

                string peerData = "";
                peerData += "------------------------\n";
                peerData += "| Peer " + peer.GetPeerId() + "\n";
                peerData += "| State " + (int)peer.GetState() + "\n";
                peerData += "| Name " + peer.GetName() + "\n";
                peerData += "| Client set?: " + (peer.GetClient()!=null ? "yes":"no") + "\n";
                peerData += "| IsLocal " + (peer.IsLocal()?"yes":"no") + "\n";
                peerData += "| IsHost " + (peer.IsHost() ? "yes" : "no") + "\n";
                peerData += "------------------------";

                Rect position = new Rect(labelX + (peerIdx * labelSpacing), labelY, labelWidth, labelHeight);
                GUI.Label(position, peerData);
            }
        }
    }
}
