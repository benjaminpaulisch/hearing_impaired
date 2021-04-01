using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;
using Assets.LSL4Unity.Scripts; // reference the LSL4Unity namespace to get access to all classes
using Microgate.Opto.API.Entities;  //I put the dlls from the OptoAPI-TstClient in the Assets folder
using Microgate.Opto.API;

// This client app is sending requests to the OptoAPI service which is a tcp listening server.   
// Both listening server and client can send messages back and forth once a communication is established.
// The server will send answers to the requests of the client.

public class OptoApiClient : MonoBehaviour
{
    public String hostIP = "127.0.0.1";
    public int hostPort = 31967;
    public LSLMarkerStream optoGaitEvents;


    private Socket socket;
    private byte[] bytes = new byte[1024];

    private char stx = (char)0x02;  //start of command
    private char etx = (char)0x03;  //end of command


    //logic handles
    private bool socketInitialized = false;


    // Start is called before the first frame update
    void Start()
    {
        
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    private bool InitSocket(string host, int port)
    {
        // Connect to a Remote server  
        // Get Host IP Address that is used to establish a connection

        IPAddress ipAddress = IPAddress.Parse(host);
        IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

        // Create the TCP/IP socket
        socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        bool returnVal = true;

        // Connect the socket to the remote endpoint. Catch any errors.    
        try
        {
            // Connect to Remote EndPoint  
            Debug.Log("Connecting to OptoGait at " + socket.RemoteEndPoint.ToString());
            optoGaitEvents.Write("Connecting to OptoGait at " + socket.RemoteEndPoint.ToString());

            socket.Connect(remoteEP);

            Debug.Log("Connection to OptoAPI successful");
            optoGaitEvents.Write("Connection to OptoAPI successful");

            socketInitialized = true;

        }
        catch (ArgumentNullException ane)
        {
            Debug.LogWarning("ArgumentNullException : " + ane.ToString());
            optoGaitEvents.Write("Error connecting to OptoAPI:\nArgumentNullException: " + ane.ToString());
            returnVal = false;
        }
        catch (SocketException se)
        {
            Debug.LogWarning("SocketException : " + se.ToString());
            optoGaitEvents.Write("Error connecting to OptoAPI:\nSocketException: " + se.ToString());
            returnVal = false;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Unexpected exception : " + e.ToString());
            optoGaitEvents.Write("Error connecting to OptoAPI:\nUnexpected exception: " + e.ToString());
            returnVal = false;
        }
        
        return returnVal;

    }//InitSocket()


    private bool CloseSocket()
    {
        // Release the socket

        bool returnVal = true;

        try
        {
            Debug.Log("Closing connection to OptoAPI...");
            optoGaitEvents.Write("Closing connection to OptoAPI...");

            socket.Shutdown(SocketShutdown.Both);
            socket.Close();

            Debug.Log("Connection closed");
            optoGaitEvents.Write("Connection closed");
        }
        catch (ArgumentNullException ane)
        {
            Debug.LogWarning("ArgumentNullException : " + ane.ToString());
            optoGaitEvents.Write("Error connecting to OptoAPI:\nArgumentNullException: " + ane.ToString());
            returnVal = false;
        }
        catch (SocketException se)
        {
            Debug.LogWarning("SocketException : " + se.ToString());
            optoGaitEvents.Write("Error connecting to OptoAPI:\nSocketException: " + se.ToString());
            returnVal = false;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Unexpected exception : " + e.ToString());
            optoGaitEvents.Write("Error connecting to OptoAPI:\nUnexpected exception: " + e.ToString());
            returnVal = false;
        }

        return returnVal;

    }


    private bool CheckConnection(String ip, int port)
    {
        //Checks if the hardware connection to the OptoGait 

        Debug.Log("Checking hardware connection to OptoGait...");
        optoGaitEvents.Write("Checking hardware connection to OptoGait...");

        SendRequest("K");

        String answer = ReceiveAnswer();

        if (answer == "")
        {
            Debug.LogWarning("No answer from OptoAPI");
            optoGaitEvents.Write("Error: No answer from OptoAPI");

            return false;
        }
        else
        {
            XElement parsedXmlString = XElement.Parse(answer);

            List<XElement> answerNodes = parsedXmlString.Elements().ToList();

            foreach (XElement node in answerNodes)
            {
                //Debug.Log(node.Name + ":" + node.Value);

                if (node.Name == "HwConnection")
                {
                    if (node.Value == "OK")
                    {
                        Debug.Log(node.Name + " to OptoGait: " + node.Value);
                        optoGaitEvents.Write(node.Name + " to OptoGait: " + node.Value);

                        return true;
                    }
                    else if (node.Value == "Fail")
                    {
                        Debug.LogWarning(node.Name + " to OptoGait: " + node.Value);
                        optoGaitEvents.Write("Error: " + node.Name + " to OptoGait: " + node.Value);

                        return false;
                    }
                }
            }
        }

        return false;


    }//CheckConnection()


    private bool SendRequest(String command)
    {
        //Sending a request to the OptoApi service

        string strMsg = stx + command + etx;
        byte[] msg = Encoding.ASCII.GetBytes(strMsg);

        Debug.Log("Sending request to OptoAPI: " + strMsg);
        optoGaitEvents.Write("Sending request to OptoAPI: " + strMsg);

        bool returnVal = true;

        try
        {
            // Send the data through the socket.    
            int bytesSent = socket.Send(msg);
        }
        catch (ArgumentNullException ane)
        {
            Debug.LogWarning("ArgumentNullException : " + ane.ToString());
            optoGaitEvents.Write("Error connecting to OptoAPI:\nArgumentNullException: " + ane.ToString());
            returnVal = false;
        }
        catch (SocketException se)
        {
            Debug.LogWarning("SocketException : " + se.ToString());
            optoGaitEvents.Write("Error connecting to OptoAPI:\nSocketException: " + se.ToString());
            returnVal = false;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Unexpected exception : " + e.ToString());
            optoGaitEvents.Write("Error connecting to OptoAPI:\nUnexpected exception: " + e.ToString());
            returnVal = false;
        }

        return returnVal;

    }//SendRequest()


    private String ReceiveAnswer()
    {
        // Receive a response from the OptoApi service
        String answer = "";

        Debug.Log("Receiving answer from OptoAPI...");
        optoGaitEvents.Write("Receiving answer from OptoAPI...");

        try
        {
            int bytesRec = socket.Receive(bytes);
            answer = Encoding.ASCII.GetString(bytes, 0, bytesRec);

            Debug.Log("Answer from OptoAPI: " + answer);
            optoGaitEvents.Write("Answer from OptoAPI: " + answer);

        }
        catch (ArgumentNullException ane)
        {
            Debug.LogWarning("ArgumentNullException : " + ane.ToString());
            optoGaitEvents.Write("Error connecting to OptoAPI:\nArgumentNullException: " + ane.ToString());
        }
        catch (SocketException se)
        {
            Debug.LogWarning("SocketException : " + se.ToString());
            optoGaitEvents.Write("Error connecting to OptoAPI:\nSocketException: " + se.ToString());
        }
        catch (Exception e)
        {
            Debug.LogWarning("Unexpected exception : " + e.ToString());
            optoGaitEvents.Write("Error connecting to OptoAPI:\nUnexpected exception: " + e.ToString());
        }

        return answer;

    }//receiveAnswer()


    public static String ConvertXmlToLslEventMarker(string xmlString)
    {
        String eventMarkerString = "";

        XElement parsedXmlString = XElement.Parse(xmlString);

        //Debug.Log("parsedXmlString: " + parsedXmlString);

        List<XElement> responseNodes = parsedXmlString.Elements().ToList();

        foreach (XElement node in responseNodes)
        {
            //Debug.Log(node.Name + ":" + node.Value);

            //append to marker string
            eventMarkerString += node.Name + ":" + node.Value + ";";

        }

        Debug.Log("eventMarkerString: " + eventMarkerString);

        return eventMarkerString;

    }//ConvertXmlToLslEventMarker()

}
