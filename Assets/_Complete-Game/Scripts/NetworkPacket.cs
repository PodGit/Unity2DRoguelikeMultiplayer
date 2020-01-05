using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class NetworkPacket
{
    public const int MAX_PACKET_SIZE = 256;

    private byte[] buffer;
    private int currentSize;

    public enum PacketType : byte
    {
        JOIN_REQUEST,
        JOIN_ACKNOWLEDGE,
        PEER_DATA
    }

    public enum DataType : byte
    {
        INTEGER,
        STRING
    }

    public NetworkPacket()
    {
        buffer = new byte[MAX_PACKET_SIZE];
        currentSize = 0;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetType(PacketType type)
    {
        Debug.Assert(currentSize == 0, "Packet isn't empty");
        buffer[0] = (byte)type;
        IncreasePacketSize(1);
    }

    void IncreasePacketSize(int numToAdd)
    {
        currentSize += numToAdd;
    }

    public void WriteInt(int inInt)
    {
        buffer[currentSize] = (byte)DataType.INTEGER;
        IncreasePacketSize(1);

        byte[] bytes = BitConverter.GetBytes(inInt);
        byte numBytes = (byte)bytes.Length;

        buffer[currentSize] = numBytes;
        IncreasePacketSize(1);

        bytes.CopyTo(buffer, currentSize);
        IncreasePacketSize(numBytes);
    }

    public void WriteString(string inString)
    {
        buffer[currentSize] = (byte)DataType.STRING;
        IncreasePacketSize(1);

        byte[] asciiBytes = Encoding.ASCII.GetBytes(inString);
        byte numBytes = (byte)asciiBytes.Length;

        buffer[currentSize] = numBytes;
        IncreasePacketSize(1);

        asciiBytes.CopyTo(buffer, currentSize);
        IncreasePacketSize(numBytes);
    }

    void GetBytes(out byte[] output, out int size)
    {
        output = buffer;
        size = currentSize;
    }
}
