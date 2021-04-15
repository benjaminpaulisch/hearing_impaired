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
//using Microgate.Opto.API.Biz;

// This client app is sending requests to the OptoAPI service which is a tcp listening server.   
// Both listening server and client can send messages back and forth once a communication is established.
// The server will send answers to the requests of the client.

public class OptoApiClient : MonoBehaviour
{
    public LSLMarkerStream optoGaitEvents;

    private Socket socket;
    private byte[] bytes = new byte[1024];

    private char stx = (char)0x02;  //start of command
    private char etx = (char)0x03;  //end of command

    private String sprintGaitConfig;
    private Microgate.Opto.API.Entities.SprintGaitConfig gc;

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


    public bool InitSocket(string host, int port)
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
            Debug.Log("Connecting to OptoGait at " + remoteEP.ToString());
            optoGaitEvents.Write("Connecting to OptoGait at " + remoteEP.ToString());

            socket.Connect(remoteEP);

            Debug.Log("Connection to OptoAPI server successful");
            optoGaitEvents.Write("Connection to OptoAPI server successful");

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


    public bool CloseSocket()
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


    public bool CheckConnection(String ip, int port)
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


    public void InitializeTest(String participantID)
    {
        //Create config
        Microgate.Opto.API.Entities.SprintGaitConfig gaitconfig = new Microgate.Opto.API.Entities.SprintGaitConfig();

        gc = new Microgate.Opto.API.Entities.SprintGaitConfig();
        gc.TestName = "GaitTest_" + participantID;
        gc.GetRawData = true;
        gc.Start = StartType.Auto;
        gc.Stop = StopType.SoftwareCommand;
        gc.Type = SprintGaitType.Gait;
        gc.ResultType = DataType.Row;
        gc.StartPosition = StartWhere.OutSideArea;
        gc.StopPosition = StopWhere.OutSideArea;
        gc.StartingFoot = Foot.NotDefined;

        string xml = Helper.Serialize<Microgate.Opto.API.Entities.SprintGaitConfig>(gc);

        /*
        sprintGaitConfig =
        "<?xml version=\"1.0\" encoding=\"utf-16\"?>" +
        "<SprintGaitConfig xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">" +
          "<TestName>GaitTest_" + participantID + "</TestName>" +
          "<PersonWeight>0</PersonWeight>" +
          "<PersonFootLength>0</PersonFootLength>" +
          "<PersonFootWidth>0</PersonFootWidth>" +
          "<GetRawData>true</GetRawData>" +
          "<AutoStartTest xsi:nil=\"true\" />" +
          "<StartTestDelay xsi:nil=\"true\" />" +
          "<CancelLastTest xsi:nil=\"true\" />" +
          "<Type>Gait</Type>" +
          "<ResultType>Row</ResultType>" +
          "<Start>SoftwareCommand</Start>" +
          "<StartPosition>OutSideArea</StartPosition>" +
          "<Stop>SoftwareCommand</Stop>" +
          "<StopPosition>OutSideArea</StopPosition>" +
          "<StartingFoot>NotDefined</StartingFoot>" +
          "<NumberOfIntermediate>0</NumberOfIntermediate>" +
          "<NumberOfStep>0</NumberOfStep>" +
          "<EnableEMGVirtualFootswitch>false</EnableEMGVirtualFootswitch>" +
          "<Template>None</Template>" +
          "<Parameters>" +
            "<MinimumContactTime>10</MinimumContactTime>" +
            "<MinimumFlightTime>10</MinimumFlightTime>" +
            "<MaximumFlightTime>0</MaximumFlightTime>" +
            "<TimeoutContactTime xsi:nil=\"true\" />" +
            "<TimeoutFlightTime xsi:nil=\"true\" />" +
            "<ExternalSignalHoldoff>500</ExternalSignalHoldoff>" +
            "<EntryPoint>Automatic</EntryPoint>" +
            "<StepLengthCalculation>Tip2Tip</StepLengthCalculation>" +
            "<MinimumGapFeet>10</MinimumGapFeet>" +
            "<MinimumFootLength>5</MinimumFootLength>" +
            "<MinimumFootWidth>3</MinimumFootWidth>" +
            "<MinimumGapFeetWidth>3</MinimumGapFeetWidth>" +
            "<DistanceSplit1>0</DistanceSplit1>" +
            "<DistanceSplit2>0</DistanceSplit2>" +
            "<TestTimeout>3000</TestTimeout>" +
            "<ReferenceSpeedAtStep3>6</ReferenceSpeedAtStep3>" +
            "<ReferenceSpeedAtStep6>8</ReferenceSpeedAtStep6>" +
            "<ReferenceSpeedAtStep9>9</ReferenceSpeedAtStep9>" +
            "<DiscardFirstStep>0</DiscardFirstStep>" +
            "<DiscardLastStep>0</DiscardLastStep>" +
            "<FootFilterAtBeginEnd>false</FootFilterAtBeginEnd>" +
          "</Parameters>" +
        "</SprintGaitConfig>"
        ;*/
        
        //send request to OptoApi
        SendRequest("I" + xml);


    }//InitializeTest()


    public void EndTest()
    {
        SendRequest("E");

    }


    public void CancelTest()
    {
        SendRequest("C");
    }


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


    public String ReceiveAnswer()
    {
        // Receive a response from the OptoApi service
        String answer = "";

        Debug.Log("Receiving answer from OptoAPI...");
        optoGaitEvents.Write("Receiving answer from OptoAPI...");

        try
        {
            int bytesRec = socket.Receive(bytes);
            answer = Encoding.ASCII.GetString(bytes, 0, bytesRec);

            Debug.Log("Answer from OptoAPI: " + ConvertXmlToLslEventMarker(answer));
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

        //Debug.Log("eventMarkerString: " + eventMarkerString);

        return eventMarkerString;

    }//ConvertXmlToLslEventMarker()

}
