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
    private PacketType type;

    private int readOffset = 0;

    public enum PacketType : byte
    {
        JOIN_REQUEST,
        JOIN_ACKNOWLEDGE,
        INIT_PEER_DATA
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

    public NetworkPacket(byte[] buffer, int size)
    {
        this.buffer = buffer;
        currentSize = size;

        Parse();
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetPacketType(PacketType type)
    {
        Debug.Assert(currentSize == 0, "Packet isn't empty");

        this.type = type;
        buffer[0] = (byte)type;
        IncreasePacketSize(1);
    }

    public PacketType GetPacketType()
    {
        return type;
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

    public string ReadString()
    {
        DataType dataType = (DataType)buffer[readOffset];
        readOffset++;

        Debug.Assert(dataType == DataType.STRING, "ReadString: Data type wasn't string");

        int numBytes = (int)buffer[readOffset];
        readOffset++;

        Debug.Log("ReadString numBytes:" + numBytes);

        byte[] stringArray = new byte[NetworkPacket.MAX_PACKET_SIZE];
        Array.Copy(buffer, readOffset, stringArray, 0, numBytes);

        readOffset += numBytes;

        return Encoding.ASCII.GetString(stringArray);
    }

    public void GetBytes(out byte[] output, out int size)
    {
        output = buffer;
        size = currentSize;
    }

    void Parse()
    {
        this.type = (PacketType)buffer[0];
        readOffset++;
    }
}
