using UnityEngine;
using UnityEngine.UI;
using Assets.LSL4Unity.Scripts; // reference the LSL4Unity namespace to get access to all classes
using LSL;

public class ExperimentManager : MonoBehaviour
{
    //##### Inspector Interface #####
    [Header("General Config")]
    public float isiDurationAvg = 1;                //1s ISI duration on average
    public float isiDurationVariation = 0.5f;       //0.5s variation (so the ISI duration range is betweeen 0.5s and 1.5s)
    public float stimulusDuration = 0.1f;           //100ms stimulus duration
    public int ledBrightness = 10;                  //should be a value 0-100 (0=off)
    public float audioVolume = 1;                   //should be a value 0-1
    public float responseTimeMax = 1.9f;            //1.5s max possible response time
    public bool debugMode = false;

    [Header("Experiment specific")]
    public int gaitPassesPerBlock = 35;             
    public int trialsPerBlock = 100;                //this is now only used for ST_sitting blocks, for DT_walking blocks the amount of trials is calculated with gaitPassesPerBlock * trialsPerGaitPass to prevent errors
    public int trialsPerGaitPass = 5;               //the maximum number of trials possible during each gait pass
    //public int trialsWalkingChunkMultiplier = 10;   //will be multiplied with the amount of different stimuli to get a chunk size for the walking conditions

    [Header("Training specific")]
    public int gaitPassesTraining = 3;

    [Header("Baseline specific")]
    public int baselineDuration = 300;              //5 minutes

    [Header("Misc")]
    public LSLMarkerStream marker;
    public OptoApiClient optoApiClient;
    public string optoApiHostIP = "127.0.0.1";
    public int optoApiHostPort = 31967;

    //##########


    // General vars
    private string[] conditions = new string[9] { "ST_walking", "ST_audio", "DT_audio", "ST_visual",  "DT_visual", "BL_sitting", "BL_walking", "Training_audio", "Training_visual" };
    private int[][] conditionSequences = new int[12][];
    private int currentConditionNo;
    private int currentSequenceNo;
    private int[] currentSequence = new int[5];
    private int currentSequenceCounter;

    private string[] visualStimuli = new string[4] { "left_yellow", "left_blue", "right_yellow", "right_blue" };
    private string[] audioStimuli = new string[4] { "left_high", "left_low", "right_high", "right_low" };
    private int[] stimuliBaseSequence = new int[] { 0, 1, 2, 3 };
    private int[] stimuliBlockSequence;
    private string currentStimulus;

    private int gaitPassCounter = 0;
    private Vector3 gaitCornerPositionOne;
    private Vector3 gaitCornerPositionTwo;
    private Vector3 gaitCornerPositionTree;
    private Vector3 gaitCornerPositionFour;
    private Vector3[] corners = new Vector3[4];     //used to store the 4 corner positions of the OptoGait you can put in in the calibration menu
    private int controllerInsideGaitCounter = 0;              //used to store the amount of controllers inside OptoGait object collider
    private int cornerCounter = 1;

    private float currentTime = 0;                  //current time value during a trial
    private float currentStimulusTime = 0;          //the time value when the stimulus was shown during the current trial
    private float currentResponseTime = 0;          //calculated response time (duration)

    [HideInInspector] // Hides var below
    public string participantID;
    private int participantAge;
    private string participantGroup;
    private string participantSex;

    private string tempMarkerText;

    //lsl streams for visual stimulus and answeres from raspi
    private liblsl.StreamInfo visualStimulusStreamInfo;
    private liblsl.StreamOutlet visualStimulusStreamOutlet;
    private liblsl.StreamInlet rasPiStreamInlet;
    private string rasPiConnected = "RasPi connected";
    private string rasPiNotConnected = "RasPi not connected";


    // Experiment specific
    private int stWalkingRunNo = 0;
    private int stAudioRunNo = 0;
    private int stVisualRunNo = 0;
    private int dtAudioRunNo = 0;
    private int dtVisualRunNo = 0;
    private float[] isiDurations;
    private int trialCounter = 0;
    private int currentTrialInGait = 0;
    //private int sequenceChunkCounter = 0;       //used only in walking conditions for isi and trials sequences
    private float currentIsiDuration;           //stores individual ISI duration of the current trial
    private string responseSide = "";
    private bool responseActive = false;
    //public static bool responseActive = false;
    private bool insideGait = false;

    // Training specific
    private int trainingVisualRunNo = 0;
    private int trainingAudioRunNo = 0;

    // Baseline specific
    private int baselineSitRunNo = 0;
    private int baselineWalkRunNo = 0;


    // Programm logic handler
    private int programStatus = 0;                  //indicating the current status of the program (main menu, training, experiment etc)
    private bool idSet = false;
    private bool ageSet = false;
    private bool groupSet = false;
    private bool sexSet = false;
    private bool configComplete = false;
    private bool cornerOneSet = false;
    private bool cornerTwoSet = false;
    private bool cornerThreeSet = false;
    private bool cornerFourSet = false;
    private bool gaitPositionsSet = false;
    private bool setGaitCornersActive = false;
    private bool optoGaitConnected = false;
    private bool responseMarkerSent = false;


    // Experiment logic handler
    private bool sequenceStarted = false;
    private bool experimentStarted = false;
    private bool expInitRun = false;
    private bool experimentEnd = false;
    private bool responseTimeOver = false;
    private bool maxGaitTrialsReached = false;

    //training logic handler
    private bool trainingStarted = false;
    private bool trainingInitRun = false;
    private bool trainingEnd = false;

    //baseline logic handler
    private bool baselineStarted = false;
    private bool baselineInitRun = false;
    private bool baselineEnd = false;

    // Trial logic handler
    private bool isiStarted = false;
    private bool stimulusShown = false;


    // Gameobject handles
    private GameObject mainMenuCanvas, configMenuCanvas, calibrationMenuCanvas, desktopInfoCanvas, expMenuCanvas, expBlockMenuCanvas,
        buttonExpMenu, buttonConnectRasPi, buttonCreateOptoGaitCube, buttonSetGaitCorners, buttonExpSequence, //buttonTraining, buttonBaselineWalking, buttonBaselineSitting, buttonSittingVisual, buttonSittingAudio, buttonWalkingST, buttonWalkingVisual, buttonWalkingAudio,
        inputParticipantID, inputParticipantAge, inputParticipantGroup, inputParticipantSex, inputSequence,
        configurationIncompleteText, calibrationIncompleteText, rasPiNotConnectedText, textCondition, textConditionRunNo, textTrialNo, textTrialInGaitNo,  textGaitPassNo, textTime,
        optoGait, optoGaitConnectionText
        ;

    public AudioSource audioSource_high, audioSource_low;

    public GameObject controllerLeft, controllerRight;




    void Start()
    {
        // Start is called before the first frame update

        //the 12 sequences from the ethics document
        conditionSequences[0] = new int[] { 0, 1, 2, 3, 4 };      //Sequence 1
        conditionSequences[1] = new int[] { 0, 1, 4, 3, 2 };      //Sequence 2
        conditionSequences[2] = new int[] { 0, 3, 2, 1, 4 };      //Sequence 3
        conditionSequences[3] = new int[] { 0, 3, 4, 1, 2 };      //Sequence 4
        conditionSequences[4] = new int[] { 2, 1, 0, 3, 4 };      //Sequence 5
        conditionSequences[5] = new int[] { 2, 1, 4, 3, 0 };      //Sequence 6
        conditionSequences[6] = new int[] { 2, 3, 0, 1, 4 };      //Sequence 7
        conditionSequences[7] = new int[] { 2, 3, 4, 1, 0 };      //Sequence 8
        conditionSequences[8] = new int[] { 4, 1, 2, 3, 0 };      //Sequence 9
        conditionSequences[9] = new int[] { 4, 1, 0, 3, 2 };      //Sequence 10
        conditionSequences[10] = new int[] { 4, 3, 2, 1, 0 };      //Sequence 11
        conditionSequences[11] = new int[] { 4, 3, 0, 1, 2 };      //Sequence 12

        // Finding the game objects:
        //marker = FindObjectOfType<LSLMarkerStream>();
        GameObject go = GameObject.Find("LSL_MarkerStream_Experiment");
        marker = go.GetComponent<LSLMarkerStream>();

        optoApiClient = FindObjectOfType<OptoApiClient>();
        mainMenuCanvas = GameObject.Find("MainMenuCanvas");
        configurationIncompleteText = GameObject.Find("ConfigurationIncompleteText");
        calibrationIncompleteText = GameObject.Find("CalibrationIncompleteText");
        buttonSetGaitCorners = GameObject.Find("ButtonSetGaitCorners");
        buttonCreateOptoGaitCube = GameObject.Find("ButtonCreateOptoGaitCube");
        buttonConnectRasPi = GameObject.Find("ButtonConnectRasPi");
        rasPiNotConnectedText = GameObject.Find("RasPiNotConnectedText");
        buttonExpMenu = GameObject.Find("ButtonExpMenu");
        buttonExpSequence = GameObject.Find("ButtonExpSequence");
        expMenuCanvas = GameObject.Find("ExpMenuCanvas");
        expBlockMenuCanvas = GameObject.Find("ExpBlockMenuCanvas");
        /*
        buttonTraining = GameObject.Find("ButtonTraining");
        buttonBaselineWalking = GameObject.Find("ButtonBaselineWalking");
        buttonBaselineSitting = GameObject.Find("ButtonBaselineSitting");
        buttonSittingVisual = GameObject.Find("ButtonSittingVisual");
        buttonSittingAudio = GameObject.Find("ButtonSittingAudio");
        buttonWalkingST = GameObject.Find("ButtonWalkingST");
        buttonWalkingVisual = GameObject.Find("ButtonWalkingVisual");
        buttonWalkingAudio = GameObject.Find("ButtonWalkingAudio");
        */
        configMenuCanvas = GameObject.Find("ConfigMenuCanvas");
        inputParticipantID = GameObject.Find("InputParticipantID");
        inputParticipantAge = GameObject.Find("InputParticipantAge");
        inputParticipantGroup = GameObject.Find("DropdownParticipantGroup");
        inputParticipantSex = GameObject.Find("DropdownParticipantSex");
        calibrationMenuCanvas = GameObject.Find("CalibrationMenuCanvas");
        desktopInfoCanvas = GameObject.Find("DesktopInfoCanvas");
        textCondition = GameObject.Find("TextCondition");
        textConditionRunNo = GameObject.Find("TextConditionRunNo");
        textTrialNo = GameObject.Find("TextTrialNo");
        textTrialInGaitNo = GameObject.Find("TextTrialInGaitNo");
        textGaitPassNo = GameObject.Find("TextGaitPassNo");
        textTime = GameObject.Find("TextTime");
        controllerLeft = GameObject.Find("Controller (left)");
        controllerRight = GameObject.Find("Controller (right)");
        optoGait = GameObject.Find("OptoGait");
        optoGaitConnectionText = GameObject.Find("OptoGaitConnectionText");
        inputSequence = GameObject.Find("DropdownExpSequence");

        if (!debugMode)
        {
            buttonExpMenu.GetComponent<Button>().interactable = false;
        }
        else
        {
            buttonExpMenu.GetComponent<Button>().interactable = true;
        }

        //start lsl stream for sending commands to RasPi (for triggering visual stimuli)
        visualStimulusStreamInfo = new liblsl.StreamInfo("HearingImpaired_Unity3D_CommandsToRasPi", "Markers", 1, 0, liblsl.channel_format_t.cf_string, "unity3dId123354");
        visualStimulusStreamOutlet = new liblsl.StreamOutlet(visualStimulusStreamInfo);

        // start the Main Menu:
        StartMainMenu();

    }//start()


    void Update()
    {
        // Update is called once per frame
        try
        {
            //try to fetch SteamVR controllers (as they tend to be available a few frames after the start)
            if (controllerLeft == null)
            {
                //Debug.Log("Controller (left) is null");
                controllerLeft = GameObject.Find("Controller (left)");
            }
            if (controllerRight == null)
            {
                //Debug.Log("Controller (right) is null");
                controllerRight = GameObject.Find("Controller (right)");
            }
            

            //Debug.Log("GetJoystickNames: " + UnityEngine.Input.GetJoystickNames());

            //exp status check
            switch (programStatus)
            {
                case 0: //main menu
                    {
                        //check if config, calibration and OptoAPI connection have been done
                        if (debugMode)
                        {
                            buttonExpMenu.GetComponent<Button>().interactable = true;
                        }
                        else
                        {
                            if (configComplete && gaitPositionsSet && optoGaitConnected)
                            {
                                buttonExpMenu.GetComponent<Button>().interactable = true;
                            }
                            else
                            {
                                buttonExpMenu.GetComponent<Button>().interactable = false;
                            }
                        }
                        break;
                    }

                case 1: //configuration
                    {
                        break;
                    }

                case 2: //calibration
                    {
                        if (setGaitCornersActive)
                        {

                        }

                        break;
                    }

                case 3: //exp menu
                    {
                        break;
                    }

                case 4: //training
                    {
                        if (Input.GetKeyDown("escape"))
                        {
                            marker.Write("training:abort");
                            Debug.Log("training:abort");

                            trainingStarted = false;

                            //go to exp menu
                            StartExpMenu();
                        }
                        else if (!trainingInitRun)
                        {
                            InitTraining();
                        }
                        else
                        {
                            //Run experiment
                            RunExperiment();
                        }

                        break;
                    }

                case 5: //experiment
                    {
                        //check for abort by pressing the escape key
                        if (Input.GetKeyDown("escape"))
                        {
                            marker.Write("experimentBlock:abort");
                            Debug.Log("experimentBlock:abort");

                            experimentStarted = false;

                            //go to exp menu
                            StartExpMenu();
                        }
                        //Initialize experiment
                        else if (!expInitRun)
                        {
                            if (currentConditionNo == 0)
                            {
                                //single task walking
                                InitWalkingST();
                                experimentStarted = true;
                            }
                            else
                            {
                                //single & dual dual task conditions
                                InitExperiment();

                                //only in single task sitting conditions
                                if (currentConditionNo == 1 || currentConditionNo == 3)
                                {
                                    experimentStarted = true;
                                }
                            }
                        }
                        else
                        {
                            //Run experiment
                            RunExperiment();
                        }

                        break;
                    }

                case 6: //baseline
                    {
                        //check for abort by pressing the escape key
                        if (Input.GetKeyDown("escape"))
                        {
                            marker.Write("baseline:abort");
                            Debug.Log("baseline:abort");
                            
                            baselineStarted = false;

                            //go to exp menu
                            StartExpMenu();
                        }
                        else if (!baselineInitRun)
                        {
                            //initialize baseline
                            InitBaseline();
                        }
                        else
                        {
                            //Run baseline
                            if (!baselineEnd)
                            {
                                currentTime += Time.deltaTime;

                                if (currentTime > baselineDuration)
                                {
                                    baselineEnd = true;

                                    //end of baseline
                                    marker.Write("baseline:end");
                                    print("baseline:end");

                                    //go to exp menu
                                    StartExpMenu();
                                }
                                else
                                {
                                    //update desktop info texts
                                    int tempNo;
                                    if (currentConditionNo == 5)
                                    {
                                        tempNo = baselineSitRunNo;
                                    }
                                    else
                                    {
                                        tempNo = baselineWalkRunNo;
                                    }

                                    SetDesktopInfoTexts(conditions[currentConditionNo], tempNo.ToString(), "-", "-", "-", string.Format("{0}:{1:00}", (int)currentTime / 60, (int)currentTime % 60));
                                }
                            }
                        }
                        break;
                    }

            }//switch()

        }
        catch (System.Exception e)  //catch errors and log them and write them to lsl stream, then throw the exception again
        {
            marker.Write(e.ToString());
            Debug.LogError(e);
            throw (e);
        }

    }//update()



    //Start methods
    public void StartMainMenu()
    {
        //This method is used for starting the main menu.
        Debug.Log("Starting Main Menu");
        programStatus = 0;

        //activate/deactivate GameObjects
        mainMenuCanvas.SetActive(true);
        expMenuCanvas.SetActive(false);
        expBlockMenuCanvas.SetActive(false);
        configMenuCanvas.SetActive(false);
        calibrationMenuCanvas.SetActive(false);
        desktopInfoCanvas.SetActive(false);


        //check if config, calibration and raspi connection have been done
        if (debugMode)
        {
            buttonExpMenu.GetComponent<Button>().interactable = true;
        }
        else
        {
            if (configComplete && gaitPositionsSet && optoGaitConnected)
            {
                buttonExpMenu.GetComponent<Button>().interactable = true;
            }
            else
            {
                buttonExpMenu.GetComponent<Button>().interactable = false;
            }
        }

        if (configComplete)
        {
            configurationIncompleteText.SetActive(false);
        }
        else
        {
            configurationIncompleteText.SetActive(true);
        }

        if (gaitPositionsSet)
        {
            calibrationIncompleteText.SetActive(false);
        }
        else
        {
            calibrationIncompleteText.SetActive(true);
        }


    }//StartMainMenu()


    public void StartExpMenu()
    {
        //This method is used for starting the experiment menu.
        Debug.Log("Starting Experiment Menu");
        programStatus = 3;

        expInitRun = false;
        baselineInitRun = false;
        trainingInitRun = false;

        //activate/deactivate GameObjects
        mainMenuCanvas.SetActive(false);
        expMenuCanvas.SetActive(true);
        expBlockMenuCanvas.SetActive(false);
        configMenuCanvas.SetActive(false);
        calibrationMenuCanvas.SetActive(false);
        desktopInfoCanvas.SetActive(false);

        CheckSequenceInput();

    }


    public void StartExpBlockMenu()
    {
        //This method is used for starting the experiment menu.
        Debug.Log("Starting Experiment Block Menu");
        programStatus = 3;

        expInitRun = false;
        baselineInitRun = false;
        trainingInitRun = false;

        //activate/deactivate GameObjects
        mainMenuCanvas.SetActive(false);
        expMenuCanvas.SetActive(false);
        expBlockMenuCanvas.SetActive(true);
        configMenuCanvas.SetActive(false);
        calibrationMenuCanvas.SetActive(false);
        desktopInfoCanvas.SetActive(false);

    }


    public void StartConfiguration()
    {
        //This method is used for the "Configuration" button on the main menu. WHen the button is pressed this method is executed.
        Debug.Log("Starting Configuration");
        programStatus = 1;

        //activate/deactivate GameObjects
        mainMenuCanvas.SetActive(false);
        expMenuCanvas.SetActive(false);
        expBlockMenuCanvas.SetActive(false);
        configMenuCanvas.SetActive(true);
        calibrationMenuCanvas.SetActive(false);
        desktopInfoCanvas.SetActive(false);


    }//StartConfiguration()


    public void ConfigurationExit()
    {
        //this is called when pressing the "Back" button in the configuration menu

        //save data from the inputs:
        
        //participantID
        if (inputParticipantID.GetComponent<InputField>().text != "")
        {
            idSet = true;
            participantID = inputParticipantID.GetComponent<InputField>().text;
        }
        else
            idSet = false;

        //participantAge
        if (inputParticipantAge.GetComponent<InputField>().text != "")
        {
            try
            {
                participantAge = int.Parse(inputParticipantAge.GetComponent<InputField>().text);
                ageSet = true;
            }
            catch (System.FormatException e)
            {
                marker.Write("FormatException: invalid input value for participant age. " + e.ToString());
                Debug.Log("FormatException: invalid input value for participant age.");
                Debug.LogException(e);
                participantAge = 0;
                inputParticipantAge.GetComponent<InputField>().text = "";
                ageSet = false;
            }
        }
        else
            ageSet = false;
        
        //participantGroup
        if (!inputParticipantGroup.GetComponent<Dropdown>().options[inputParticipantGroup.GetComponent<Dropdown>().value].text.Equals("?"))
        {
            groupSet = true;
            participantGroup = inputParticipantGroup.GetComponent<Dropdown>().options[inputParticipantGroup.GetComponent<Dropdown>().value].text;
        }
        else
            groupSet = false;

        //participantSex
        if (!inputParticipantSex.GetComponent<Dropdown>().options[inputParticipantSex.GetComponent<Dropdown>().value].text.Equals("?"))
        {
            sexSet = true;
            participantSex = inputParticipantSex.GetComponent<Dropdown>().options[inputParticipantSex.GetComponent<Dropdown>().value].text;
        }
        else
            sexSet = false;
        
        /*
        Debug.Log("participantID: " + participantID + " InputField.text: " + inputParticipantID.GetComponent<InputField>().text);
        Debug.Log("participantAge: " + participantAge.ToString() + " InputField.text: " + inputParticipantAge.GetComponent<InputField>().text);
        Debug.Log("participantGroup: " + participantGroup + " InputField.text: " + inputParticipantGroup.GetComponent<Dropdown>().options[inputParticipantGroup.GetComponent<Dropdown>().value].text);
        Debug.Log("participantSex: " + participantSex + " InputField.text: " + inputParticipantSex.GetComponent<Dropdown>().options[inputParticipantSex.GetComponent<Dropdown>().value].text);
        */


        //check if config is complete
        if (idSet && ageSet && groupSet && sexSet)
        {
            configComplete = true;
        }
        else
        {
            configComplete = false;
        }

        //Go back to main menu
        StartMainMenu();

    }//ConfigurationExit()


    public void StartCalibration()
    {
        //This method is used for the "Configuration" button on the main menu. WHen the button is pressed this method is executed.
        Debug.Log("Starting Calibration");
        programStatus = 2;

        //activate/deactivate GameObjects
        mainMenuCanvas.SetActive(false);
        expMenuCanvas.SetActive(false);
        expBlockMenuCanvas.SetActive(false);
        configMenuCanvas.SetActive(false);
        calibrationMenuCanvas.SetActive(true);
        desktopInfoCanvas.SetActive(false);

        if (!(cornerOneSet && cornerTwoSet && cornerThreeSet && cornerFourSet))
        {
            buttonCreateOptoGaitCube.GetComponent<Button>().interactable = false;
        }

    }//StartCalibration()


    public void CalibrationExit()
    {
        //this is called when pressing the "Back" button in the calibration menu



        //Go back to main menu
        StartMainMenu();
    }


    public void StartTraining(int conditionNo)
    {
        //This method is used for the "Start Training" button on the main menu. When the button is pressed this method is executed.
        marker.Write("Main menu: Start " + conditions[conditionNo] + " button pressed");
        Debug.Log("Starting " + conditions[conditionNo]);
        programStatus = 4;

        currentConditionNo = conditionNo;

        //activate/deactivate GameObjects
        mainMenuCanvas.SetActive(false);
        expMenuCanvas.SetActive(false);
        expBlockMenuCanvas.SetActive(false);
        configMenuCanvas.SetActive(false);
        calibrationMenuCanvas.SetActive(false);
        desktopInfoCanvas.SetActive(true);


    }//StartTraining()


    public void StartBaseline(int conditionNo)
    {
        //This method is used for all "Start Baseline" buttons in the main menu. If one of these buttons is pressed this method is executed.
        marker.Write("Main menu: Start " + conditions[conditionNo] + " button pressed");
        Debug.Log("Starting " + conditions[conditionNo]);
        programStatus = 6;

        currentConditionNo = conditionNo;

        //activate/deactivate GameObjects
        mainMenuCanvas.SetActive(false);
        expMenuCanvas.SetActive(false);
        expBlockMenuCanvas.SetActive(false);
        configMenuCanvas.SetActive(false);
        calibrationMenuCanvas.SetActive(false);
        desktopInfoCanvas.SetActive(true);

    }


    public void StartExperiment(int conditionNo)
    {
        //This method is used for all "Start Block" buttons in the main menu. If one of these buttons is pressed this method is executed.
        marker.Write("Main menu: Start Block: " + conditions[conditionNo] + " button pressed");
        Debug.Log("Starting Experiment Block: " + conditions[conditionNo]);
        programStatus = 5;

        currentConditionNo = conditionNo;

        //activate/deactivate GameObjects
        mainMenuCanvas.SetActive(false);
        expMenuCanvas.SetActive(false);
        expBlockMenuCanvas.SetActive(false);
        configMenuCanvas.SetActive(false);
        calibrationMenuCanvas.SetActive(false);
        desktopInfoCanvas.SetActive(true);

    }//StartExperiment()


    public void StartExperimentSequence()
    {
        //This method is run when pressing the "Start Exp Sequence" button in the experiment menu.
        marker.Write("Experiment menu: Start Exp Sequence button pressed");
        Debug.Log("Starting Experiment Sequence...");

        programStatus = 5;

        //get sequence number from dropdown
        currentSequenceNo = inputSequence.GetComponent<Dropdown>().value;
        currentSequence = conditionSequences[currentSequenceNo - 1];

        marker.Write("StartExpSequence:" + currentSequenceNo.ToString());
        Debug.Log("Current Sequence: " + currentSequenceNo.ToString());

        //currentSequence = conditionSequences[currentSequenceNo-1];
        currentSequenceCounter = 0;
        sequenceStarted = true;

        //set current condition
        currentConditionNo = conditionSequences[currentSequenceNo - 1][currentSequenceCounter];

        //activate/deactivate GameObjects
        mainMenuCanvas.SetActive(false);
        expMenuCanvas.SetActive(false);
        expBlockMenuCanvas.SetActive(false);
        configMenuCanvas.SetActive(false);
        calibrationMenuCanvas.SetActive(false);
        desktopInfoCanvas.SetActive(true);

    }//StartExperimentSequence()


    //Init methods

    /*
    void InitExperimentSitting()
    {
        //supposed to only run once in the beginning of an experiment run

        Debug.Log("InitExperimentSitting()");

        experimentEnd = false;
        trialCounter = 0;


        int currentConditionCounter;
        if (conditions[currentConditionNo].Contains("visual"))
        {
            //create trial sequence for the block
            stimuliBlockSequence = CreateTrialSequenceArray(trialsPerBlock, stimuliBaseSequence, visualStimuli);

            //increment condition counter
            stVisualRunNo += 1;
            currentConditionCounter = stVisualRunNo;
        }
        else
        {
            //create trial sequence for the block
            stimuliBlockSequence = CreateTrialSequenceArray(trialsPerBlock, stimuliBaseSequence, audioStimuli);

            //increment condition counter
            stAudioRunNo += 1;
            currentConditionCounter = stAudioRunNo;
        }

        //Create isi durations for the block
        isiDurations = CreateDurationsArray(trialsPerBlock, isiDurationAvg, isiDurationVariation);


        //write experiment start marker
        tempMarkerText =
            "experiment:start;" +
            "condition:" + conditions[currentConditionNo] + ";" +
            "runNo:" + currentConditionCounter.ToString() + ";" +
            "trialsTotal:" + trialsPerBlock.ToString() + ";" +
            "isiDurationAvg:" + isiDurationAvg.ToString() + ";" +
            "isiDurationVariation:" + isiDurationVariation.ToString() + ";" +
            "responseTimeMax:" + responseTimeMax.ToString();
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);

        //write participant info (from configuration menu)
        tempMarkerText =
            "participantID:" + participantID + ";" +
            "participantAge:" + participantAge.ToString() + ";" +
            "participantGroup:" + participantGroup + ";" +
            "participantSex:" + participantSex;
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);

        //write calibration info (from calibration menu)
        tempMarkerText =
            "gaitCornerPos1:" + gaitCornerPositionOne.ToString() + ";" +
            "gaitCornerPos2:" + gaitCornerPositionTwo.ToString() + ";" +
            "gaitCornerPos3:" + gaitCornerPositionTree.ToString() + ";" +
            "gaitCornerPos4:" + gaitCornerPositionFour.ToString();
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);


        //set desktop info texts
        SetDesktopInfoTexts(conditions[currentConditionNo], currentConditionCounter.ToString(), "", "-" , "-");

        expInitRun = true;

        //start first trial
        StartTrial();

    }//InitExperimentSitting()


    void InitExperimentWalking()
    {
        //supposed to only run once in the beginning of an experiment run

        experimentEnd = false;
        trialCounter = 0;
        gaitPassCounter = 0;


        //Create trial sequence: in walking conditions we don't know the amount of trials geforehand so we calculate the trial sequence and isi durations in chunks
        //During the block we always check if we are at the end of the sequence and then randomize the sequence anew and reset start the sequence from the beginning
        int currentConditionCounter;
        if (conditions[currentConditionNo].Contains("visual"))
        {
            //create trial sequence for the block
            stimuliBlockSequence = CreateTrialSequenceArray(trialsPerBlock, stimuliBaseSequence, visualStimuli);

            //increment condition counter
            dtVisualRunNo += 1;
            currentConditionCounter = dtVisualRunNo;
        }
        else
        {
            //create trial sequence for the block
            stimuliBlockSequence = CreateTrialSequenceArray(trialsPerBlock, stimuliBaseSequence, audioStimuli);

            //increment condition counter
            dtAudioRunNo += 1;
            currentConditionCounter = dtAudioRunNo;
        }

        //Create isi durations for the block
        isiDurations = CreateDurationsArray(trialsPerBlock, isiDurationAvg, isiDurationVariation);


        //write experiment start marker
        tempMarkerText =
            "experiment:start;" +
            "condition:" + conditions[currentConditionNo] + ";" +
            "runNo:" + currentConditionCounter.ToString() + ";" +
            "gaitPasses:" + gaitPassesPerBlock.ToString() + ";" +
            "isiDurationAvg:" + isiDurationAvg.ToString() + ";" +
            "isiDurationVariation:" + isiDurationVariation.ToString() + ";" +
            "responseTimeMax:" + responseTimeMax.ToString();
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);

        //write participant info (from configuration menu)
        tempMarkerText =
            "participantID:" + participantID + ";" +
            "participantAge:" + participantAge.ToString() + ";" +
            "participantGroup:" + participantGroup + ";" +
            "participantSex:" + participantSex;
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);

        //write calibration info (from calibration menu)
        tempMarkerText =
            "gaitCornerPos1:" + gaitCornerPositionOne.ToString() + ";" +
            "gaitCornerPos2:" + gaitCornerPositionTwo.ToString() + ";" +
            "gaitCornerPos3:" + gaitCornerPositionTree.ToString() + ";" +
            "gaitCornerPos4:" + gaitCornerPositionFour.ToString();
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);

        //set desktop info texts
        SetDesktopInfoTexts(conditions[currentConditionNo], currentConditionCounter.ToString(), "", "", "-");

        expInitRun = true;

    }//InitExperimentWalking()
    */

    void InitExperiment()
    {
        //supposed to only run once in the beginning of an experiment run

        Debug.Log("InitExperiment()");

        experimentEnd = false;
        trialCounter = 0;
        gaitPassCounter = 0;
        maxGaitTrialsReached = false;

        string tempGaitPasses = "-";

        // increment condition counter
        int currentConditionCounter;
        if (currentConditionNo == 1)
        {
            stAudioRunNo += 1;
            currentConditionCounter = stAudioRunNo;
        }
        else if (currentConditionNo == 2)
        {
            dtAudioRunNo += 1;
            currentConditionCounter = dtAudioRunNo;

            tempGaitPasses = gaitPassesPerBlock.ToString();
        }
        else if (currentConditionNo == 3)
        {
            stVisualRunNo += 1;
            currentConditionCounter = stVisualRunNo;
        }
        else //(currentConditionNo == 4)
        {
            dtVisualRunNo += 1;
            currentConditionCounter = dtVisualRunNo;

            tempGaitPasses = gaitPassesPerBlock.ToString();
        }


        //create trial sequence for the block
        if (conditions[currentConditionNo].Contains("DT"))
        {
            //DT_walking conditions

            if (conditions[currentConditionNo].Contains("visual"))
            {
                //DT_visual

                //create trial sequence for the block
                stimuliBlockSequence = CreateTrialSequenceArray(gaitPassesPerBlock * trialsPerGaitPass, stimuliBaseSequence, visualStimuli);
            }
            else
            {
                //DT_audio

                //create trial sequence for the block
                stimuliBlockSequence = CreateTrialSequenceArray(gaitPassesPerBlock * trialsPerGaitPass, stimuliBaseSequence, audioStimuli);
            }

            //Create isi durations for the block
            isiDurations = CreateDurationsArray(gaitPassesPerBlock * trialsPerGaitPass, isiDurationAvg, isiDurationVariation);


            //initialize OptoGait measurement
            //optoApiClient.InitializeTest(participantID);

        }
        else
        {
            //ST_sitting conditions
            if (conditions[currentConditionNo].Contains("visual"))
            {
                //ST_visual

                //create trial sequence for the block
                stimuliBlockSequence = CreateTrialSequenceArray(trialsPerBlock, stimuliBaseSequence, visualStimuli);
            }
            else
            {
                //ST_audio

                //create trial sequence for the block
                stimuliBlockSequence = CreateTrialSequenceArray(trialsPerBlock, stimuliBaseSequence, audioStimuli);
            }

            //Create isi durations for the block
            isiDurations = CreateDurationsArray(trialsPerBlock, isiDurationAvg, isiDurationVariation);

        }


        //write experiment start marker
        tempMarkerText =
            "experimentBlock:start;" +
            "condition:" + conditions[currentConditionNo] + ";" +
            "runNo:" + currentConditionCounter.ToString() + ";" +
            "trialsTotal:" + trialsPerBlock.ToString() + ";" +
            "gaitsTotal:" + tempGaitPasses + ";" +
            "isiDurationAvg:" + isiDurationAvg.ToString() + ";" +
            "isiDurationVariation:" + isiDurationVariation.ToString() + ";" +
            "responseTimeMax:" + responseTimeMax.ToString();
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);

        //write participant info (from configuration menu)
        tempMarkerText =
            "participantID:" + participantID + ";" +
            "participantAge:" + participantAge.ToString() + ";" +
            "participantGroup:" + participantGroup + ";" +
            "participantSex:" + participantSex;
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);

        //write calibration info (from calibration menu)
        tempMarkerText =
            "gaitCornerPos1:" + gaitCornerPositionOne.ToString() + ";" +
            "gaitCornerPos2:" + gaitCornerPositionTwo.ToString() + ";" +
            "gaitCornerPos3:" + gaitCornerPositionTree.ToString() + ";" +
            "gaitCornerPos4:" + gaitCornerPositionFour.ToString();
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);


        //set desktop info texts
        SetDesktopInfoTexts(conditions[currentConditionNo], currentConditionCounter.ToString(), "", "", "", "-");

        expInitRun = true;

        
        //start first trial (only in ST conditions!)
        if (conditions[currentConditionNo].Contains("ST"))
        {
            StartTrial();
        }

    }//InitExperiment()



    void InitBaseline()
    {
        //supposed to only run once in the beginning of a baseline

        baselineEnd = false;
        currentTime = 0;
        maxGaitTrialsReached = false;

        int currentConditionCounter;
        if (currentConditionNo == 5)
        {
            baselineSitRunNo += 1;
            currentConditionCounter = baselineSitRunNo;

        }
        else //currentConditionNo == 6
        {
            baselineWalkRunNo += 1;
            currentConditionCounter = baselineWalkRunNo;

        }


        //write baseline start marker
        tempMarkerText =
        "baseline:start;" +
        "condition:" + conditions[currentConditionNo] + ";" +
        "runNo:" + currentConditionCounter.ToString() + ";" +
        "duration:" + baselineDuration.ToString();
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);

        //write participant info (from configuration menu)
        tempMarkerText =
            "participantID:" + participantID + ";" +
            "participantAge:" + participantAge.ToString() + ";" +
            "participantGroup:" + participantGroup + ";" +
            "participantSex:" + participantSex;
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);

        //write calibration info (from calibration menu)
        tempMarkerText =
            "gaitCornerPos1:" + gaitCornerPositionOne.ToString() + ";" +
            "gaitCornerPos2:" + gaitCornerPositionTwo.ToString() + ";" +
            "gaitCornerPos3:" + gaitCornerPositionTree.ToString() + ";" +
            "gaitCornerPos4:" + gaitCornerPositionFour.ToString();
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);


        //set desktop info texts
        SetDesktopInfoTexts(conditions[currentConditionNo], currentConditionCounter.ToString(), "-", "-", "-", string.Format("{0}:{1:00}", (int)currentTime / 60, (int)currentTime % 60));


        baselineInitRun = true;

    }//InitBaseline()


    void InitTraining()
    {
        //supposed to only run once in the beginning of a training run

        experimentEnd = false;
        trialCounter = 0;
        gaitPassCounter = 0;
        maxGaitTrialsReached = false;

        int currentConditionCounter;

        //create trial sequence for the block
        if (currentConditionNo == 8)
        {
            //Training_visual

            trainingVisualRunNo += 1;
            currentConditionCounter = trainingVisualRunNo;

            //create trial sequence for the block
            stimuliBlockSequence = CreateTrialSequenceArray(gaitPassesTraining * trialsPerGaitPass, stimuliBaseSequence, visualStimuli);
        }
        else if (currentConditionNo == 7)
        {
            //Training_audio

            trainingAudioRunNo += 1;
            currentConditionCounter = trainingAudioRunNo;

            //create trial sequence for the block
            stimuliBlockSequence = CreateTrialSequenceArray(gaitPassesTraining * trialsPerGaitPass, stimuliBaseSequence, audioStimuli);
        }
        else
        {
            currentConditionCounter = 0;
        }

        //Create isi durations for the block
        isiDurations = CreateDurationsArray(gaitPassesTraining * trialsPerGaitPass, isiDurationAvg, isiDurationVariation);


        //write experiment start marker
        tempMarkerText =
            "experimentBlock:start;" +
            "condition:" + conditions[currentConditionNo] + ";" +
            "runNo:" + currentConditionCounter.ToString() + ";" +
            "gaitsTotal:" + gaitPassesTraining + ";" +
            "isiDurationAvg:" + isiDurationAvg.ToString() + ";" +
            "isiDurationVariation:" + isiDurationVariation.ToString() + ";" +
            "responseTimeMax:" + responseTimeMax.ToString();
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);

        //write participant info (from configuration menu)
        tempMarkerText =
            "participantID:" + participantID + ";" +
            "participantAge:" + participantAge.ToString() + ";" +
            "participantGroup:" + participantGroup + ";" +
            "participantSex:" + participantSex;
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);

        //write calibration info (from calibration menu)
        tempMarkerText =
            "gaitCornerPos1:" + gaitCornerPositionOne.ToString() + ";" +
            "gaitCornerPos2:" + gaitCornerPositionTwo.ToString() + ";" +
            "gaitCornerPos3:" + gaitCornerPositionTree.ToString() + ";" +
            "gaitCornerPos4:" + gaitCornerPositionFour.ToString();
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);


        //set desktop info texts
        SetDesktopInfoTexts(conditions[currentConditionNo], currentConditionCounter.ToString(), "", "", "", "-");

        trainingInitRun = true;

    }//InitTraining()


    void InitWalkingST()
    {
        //supposed to only run once in the beginning of an experiment run

        experimentEnd = false;
        //trialCounter = 0;
        gaitPassCounter = 0;
        maxGaitTrialsReached = false;


        stWalkingRunNo += 1;

        //write experiment start marker
        tempMarkerText =
            "experimentBlock:start;" +
            "condition:" + conditions[currentConditionNo] + ";" +
            "runNo:" + stWalkingRunNo.ToString() + ";" +
            "gaitsTotal:" + gaitPassesPerBlock.ToString();
           
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);

        //write participant info (from configuration menu)
        tempMarkerText =
            "participantID:" + participantID + ";" +
            "participantAge:" + participantAge.ToString() + ";" +
            "participantGroup:" + participantGroup + ";" +
            "participantSex:" + participantSex;
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);

        //write calibration info (from calibration menu)
        tempMarkerText =
            "gaitCornerPos1:" + gaitCornerPositionOne.ToString() + ";" +
            "gaitCornerPos2:" + gaitCornerPositionTwo.ToString() + ";" +
            "gaitCornerPos3:" + gaitCornerPositionTree.ToString() + ";" +
            "gaitCornerPos4:" + gaitCornerPositionFour.ToString();
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);


        //set desktop info texts
        SetDesktopInfoTexts(conditions[currentConditionNo], stWalkingRunNo.ToString(), "-", "-", "", "-");


        //initialize OptoGait measurement
        //optoApiClient.InitializeTest(participantID);


        expInitRun = true;

    }//InitWalkingST()


    private void NextBlock()
    {
        //increment sequence counter
        currentSequenceCounter += 1;

        //reset vars
        experimentEnd = false;
        expInitRun = false;

        //check if sequence is finished
        if (currentSequenceCounter == conditionSequences[currentSequenceNo].Length)
        {
            //end sequence run
            print("Sequence end");

            StartExpMenu();

        }
        else
        {
            //start next block
            print("Starting next block: " + conditions[currentSequence[currentSequenceCounter]]);
            
            StartExperiment(currentSequence[currentSequenceCounter]);

        }
        

    }//NextBlock()


    void RunExperiment()
    {
        //controls all trials during an experiment run

        //Debug.Log("RunExperiment()");
        //Debug.Log("experimentStarted: " + BoolToString(experimentStarted));


        // ### For all walking conditions: check if inside gait first and only start trial if new inside
        //if (currentConditionNo == 0 || currentConditionNo == 2 || currentConditionNo == 4)
        //if ((currentConditionNo == 2 || currentConditionNo == 4) && !experimentEnd) //not condition ST_walking!
        if ((currentConditionNo == 2 || currentConditionNo == 4 || currentConditionNo == 7 || currentConditionNo == 8) && !experimentEnd) //added trainings
        {
            //check if participant is inside the OptoGait
            //if (controllerInsideGaitCounter == 2)
            if (insideGait)
            {
                //check if it's a new inside gait
                if (!experimentStarted)
                {
                    // If exp was not started before we have a new inside gait!

                    // Then we need to start a new trial
                    StartTrial();

                }

                experimentStarted = true;

            }
            else
            {
                //if not inside OptoGait

                //abort current trial if it's a new outside gait
                if (experimentStarted)
                {
                    //abort current trial
                    experimentStarted = false;

                    //end OptoGait measurement (not during training)
                    if (!trainingStarted)
                    {
                        //optoApiClient.EndTest();

                    }


                    //check if current gait was the last
                    if (currentConditionNo == 2 || currentConditionNo == 4) 
                    {
                        if (gaitPassCounter == gaitPassesPerBlock)
                        {
                            //set flag for experiment end
                            experimentEnd = true;
                            print("experimentEnd = true");
                        }
                    }
                    else if (currentConditionNo == 7 || currentConditionNo == 8)
                    {
                        if (gaitPassCounter == gaitPassesTraining)
                        {
                            //set flag for experiment end
                            experimentEnd = true;
                            print("experimentEnd = true");
                        }
                    }
                    else 
                    {
                        //current gait was not the last

                        //initialize new Optogait measurement for next gait (not during training)
                        if (!trainingStarted)
                        {
                            //optoApiClient.InitializeTest(participantID);

                        }
                        

                        if (!maxGaitTrialsReached)  //only abort if max gait trials were not reached
                        {
                            //lsl marker
                            marker.Write("trialAbort:" + trialCounter.ToString());
                            Debug.Log("trial aborted! TrialNo:" + trialCounter.ToString());

                            //go to next trial
                            NextTrial();
                        }
                    }

                }
            }

        }


        //if (experimentStarted)
        if (experimentStarted && !maxGaitTrialsReached && !experimentEnd)     //only if expriment is running and max gait trials have not been reached
        {
            //update currentTime (adding the time taken to render last frame)
            currentTime += Time.deltaTime;

            // run all trials
            if (currentConditionNo == 0)
            {
                //single task walking

                if (gaitPassCounter > gaitPassesPerBlock)
                {
                    //set flag for experiment end
                    experimentEnd = true;
                    print("experimentEnd = true");
                }

                //update desktop info texts
                SetDesktopInfoTexts(conditions[currentConditionNo], stWalkingRunNo.ToString(), "-", "-", gaitPassCounter.ToString(), "-");

            } 
            //else if (currentConditionNo == 2 || currentConditionNo == 4)
            else if (currentConditionNo == 2 || currentConditionNo == 4 || currentConditionNo == 7 || currentConditionNo == 8)
            {
                //dual task walking conditions
                if (gaitPassCounter <= gaitPassesPerBlock)
                {
                    RunTrial();
                }

                //update desktop info texts
                int tempRunNo;
                /*
                if (currentConditionNo == 2)
                {
                    tempRunNo = dtAudioRunNo;
                }
                else
                {
                    tempRunNo = dtVisualRunNo;
                }
                */

                switch (currentConditionNo)
                {
                    case 2:
                        {
                            tempRunNo = dtAudioRunNo;
                            break;
                        }
                    case 4:
                        {
                            tempRunNo = dtVisualRunNo;
                            break;
                        }
                    case 7:
                        {
                            tempRunNo = trainingAudioRunNo;
                            break;
                        }
                    case 8:
                        {
                            tempRunNo = trainingVisualRunNo;
                            break;
                        }
                    default:
                        {
                            tempRunNo = 0;
                            break;
                        }
                }

                SetDesktopInfoTexts(conditions[currentConditionNo], tempRunNo.ToString(), trialCounter.ToString(), currentTrialInGait.ToString(), gaitPassCounter.ToString(), "-");

            }
            else if (currentConditionNo == 1 || currentConditionNo == 3 )      
            {
                //single task sitting conditions
                //if (trialCounter < trialsPerBlock && !experimentEnd)
                if (trialCounter <= trialsPerBlock && !experimentEnd)
                {
                    RunTrial();
                }

                //update desktop info texts
                int tempRunNo;
                if (currentConditionNo == 1)
                {
                    tempRunNo = stAudioRunNo;
                }
                else
                {
                    tempRunNo = stVisualRunNo;
                }

                SetDesktopInfoTexts(conditions[currentConditionNo], tempRunNo.ToString(), trialCounter.ToString(), "-", "-", "-");

            }

        }//if experimentStarted


        // after all trials are finished
        if (experimentEnd)
        {
            //write specific end marker
            if (trainingStarted)
            {
                marker.Write("training:end");
                Debug.Log("training:end");
            }
            else
            {
                marker.Write("experimentBlock:end");
                Debug.Log("experimentBlock:end");
            }

            //activate experiment end text
            //end.SetActive(true);

            experimentStarted = false;

            //if sequece -> go to next block
            if (sequenceStarted)
            {
                NextBlock();
            }
            else if(trainingStarted || baselineStarted)
            {
                //go to exp menu
                StartExpMenu();
            }
            else
            {
                //go to exp block menu
                StartExpBlockMenu();
            }
            
        }

    }//RunExperiment()


    void RunTrial()
    {
        //controls the trial events

        //Debug.Log("RunTrial() currentTime: " + currentTime.ToString());

        //Start of trial: ISI
        if (currentTime <= currentIsiDuration)
        {
            //Debug.Log("currentIsiDuration: " + currentIsiDuration.ToString());

            if (!isiStarted)
            {
                isiStarted = true;
                marker.Write("ISI started");
                Debug.Log("ISI started: " + currentTime.ToString());
            }
        }

        //After ISI -> trigger a stimulus (audio/visual)
        if (currentTime > currentIsiDuration && !stimulusShown)
        {
            //ISI ended
            isiStarted = false;
            marker.Write("ISI ended");
            Debug.Log("ISI ended: " + currentTime.ToString());

            //trigger stimulus
            //if (currentConditionNo == 1 || currentConditionNo == 2)
            if (currentConditionNo == 1 || currentConditionNo == 2 || currentConditionNo == 7)
            {
                //audio stimuli

                string stimulusSide = "";
                string stimulusPitch = "";
                    
                if (currentStimulus.Contains("left"))
                {
                    stimulusSide = "left";
                }
                else if (currentStimulus.Contains("right"))
                {
                    stimulusSide = "right";
                }

                if (currentStimulus.Contains("high"))
                {
                    stimulusPitch = "high";
                }
                else if (currentStimulus.Contains("low"))
                {
                    stimulusPitch = "low";
                }

                TriggerAudioStimulus(stimulusSide, stimulusPitch);

            }
            //else if (currentConditionNo == 3 || currentConditionNo == 4)
            else if (currentConditionNo == 3 || currentConditionNo == 4 || currentConditionNo == 8)
            {
                //visual stimuli

                string stimulusSide = "";
                string stimulusColor = "";
                    
                if (currentStimulus.Contains("left"))
                {
                    stimulusSide = "left";
                }
                else if (currentStimulus.Contains("right"))
                {
                    stimulusSide = "right";
                }

                if (currentStimulus.Contains("yellow"))
                {
                    stimulusColor = "yellow";   //rbg values instead?
                }
                else if (currentStimulus.Contains("blue"))
                {
                    stimulusColor = "blue";     //rgb values instead?
                }

                TriggerVisualStimulus(stimulusSide, stimulusColor);

            }

            //Debug.Log("Stimulus activated: " + currentStimulusObj.name + " " + actualTime.ToString());

            //set time when stimulus was triggert
            currentStimulusTime = currentTime;

            stimulusShown = true;

        }

        //After stimulus -> wait and check for response
        if (currentTime > currentIsiDuration + stimulusDuration)
        {
            //activate response
            if (!responseActive)
            {
                responseActive = true;
                print("responseActive: true");
            }


            //check for response
            if (responseSide != "")
            {
                print("response at " + currentTime.ToString());

                //calculate if response is correct or not? ToDo

                //write lsl marker
                if (!responseMarkerSent)
                {
                    tempMarkerText =
                        "response:" + responseSide + ";" +
                        "duration:" + currentResponseTime;
                    marker.Write(tempMarkerText);
                    Debug.Log(tempMarkerText);

                    responseMarkerSent = true;
                }

                //go to next trial
                //in walking conditions check trial in gait counter
                //if (currentConditionNo == 2 || currentConditionNo == 4)
                if (currentConditionNo == 2 || currentConditionNo == 4 || currentConditionNo == 7 || currentConditionNo == 8)
                {
                    //if max trials in gait is not reached
                    if (currentTrialInGait < trialsPerGaitPass)
                    {
                        NextTrial();
                    }
                }
                else
                {
                    NextTrial();
                }

            }

        }

        if (currentTime > currentIsiDuration + stimulusDuration + responseTimeMax)
        {
            //response time over
            /*
            if (!responseTimeOver) 
            {
                responseTimeOver = true;    //to make sure this done only once
                */
                //write lsl marker
                marker.Write("response time over");
                Debug.Log("response time over. " + currentTime.ToString());

                //go to next trial
                /*
                //in walking conditions check trial in gait counter
                if (currentConditionNo == 2 || currentConditionNo == 4)
                {
                    //if max trials in gait is not reached
                    if (currentTrialInGait < trialsPerGaitPass)
                    {
                        NextTrial();
                    }
                    else
                    {
                        //write lsl marker
                        marker.Write("Max number of trials in gait reached. Waiting for next gait.");
                        print("Max number of trials in gait reached. Waiting for next gait.");
                    }
                }
                else
                {*/
                    NextTrial();
                //}

            //}

        }

    }//RunTrial()


    void NextTrial()
    {
        //controls transition to the next trial or end of the block

        //send trial end marker
        marker.Write("trialEnd:" + trialCounter.ToString());
        Debug.Log("trialEnd:" + trialCounter.ToString());

        //trialCounter += 1;

        /*
        //in walking conditions also increment trial in gait counter
        if (currentConditionNo == 2 || currentConditionNo == 4)
        {
            currentTrialInGait += 1;
        }*/


        //reset vars
        currentTime = 0.0f;
        responseActive = false;
        print("responseActive: false");

        //check if block end condition is fullfilled
        //if (currentConditionNo == 2 || currentConditionNo == 4)
        if (currentConditionNo == 2 || currentConditionNo == 4 || currentConditionNo == 7 || currentConditionNo == 8)
        {
            //dual task walking conditions

            //check if max trials in gait is not reached
            if (currentTrialInGait < trialsPerGaitPass)
            {
                //start next trial if exp is running (if NOT we could be ouside gait and don't want to start a new trial!)
                if (experimentStarted)
                {
                    StartTrial();
                }
            }
            else
            {
                maxGaitTrialsReached = true;

                //write lsl marker
                marker.Write("Max number of trials in gait reached. Waiting for next gait.");
                print("Max number of trials in gait reached. Waiting for next gait.");

                //check if max gaits in block is also reached
                if (gaitPassCounter == gaitPassesPerBlock)
                {
                    //set flag for experiment end and don't start another trial
                    experimentEnd = true;
                }
            }

        }
        else if (currentConditionNo == 1 || currentConditionNo == 3)
        {
            //single task sitting conditions
            if (trialCounter == trialsPerBlock)
            {
                //set flag for experiment end and don't start another trial
                experimentEnd = true;

            }
            else
            {
                //start next trial
                StartTrial();
            }

        }
        /*
        else
        {
            //start next trial
            StartTrial();
        }
        */

    }//NextTrial()


    void StartTrial()
    {
        //initializes the start of a trial

        //reset trial vars
        currentTime = 0.0f;
        stimulusShown = false;
        responseSide = "";
        responseTimeOver = false;
        responseMarkerSent = false;

        //increment trial counter
        trialCounter += 1;

        //differentiate between sitting and walking conditions
        switch (currentConditionNo)
        {
            /*
            case 0: //ST_walking
                {
                    break;
                }*/
            case 1: //ST_audio
                {
                    currentStimulus = audioStimuli[stimuliBlockSequence[trialCounter-1]];

                    currentIsiDuration = isiDurations[trialCounter-1];

                    break;
                }
            case 7: //Training_audio
            case 2: //DT_audio
                {
                    /*
                    //check if sequence is finished -> start new sequence
                    if (sequenceChunkCounter >= stimuliBlockSequence.Length)
                    {
                        //reshuffle sequences
                        RandomizeArray.ShuffleArray(stimuliBlockSequence);
                        RandomizeArray.ShuffleArray(isiDurations);

                        //reset counter
                        sequenceChunkCounter = 0;
                    }*/

                    //currentStimulus = audioStimuli[stimuliBlockSequence[sequenceChunkCounter]];
                    currentStimulus = audioStimuli[stimuliBlockSequence[trialCounter-1]];

                    //currentIsiDuration = isiDurations[sequenceChunkCounter];
                    currentIsiDuration = isiDurations[trialCounter-1];

                    //increment trial in gait counter
                    currentTrialInGait += 1;

                    break;
                }
            case 3: //ST_visual
                {
                    currentStimulus = visualStimuli[stimuliBlockSequence[trialCounter-1]];

                    currentIsiDuration = isiDurations[trialCounter-1];

                    break;
                }
            case 8: //Training_visual
            case 4: //DT_visual
                {
                    /*
                    //check if sequence is finished -> start new sequence
                    if (sequenceChunkCounter >= stimuliBlockSequence.Length)
                    {
                        //reshuffle sequence
                        RandomizeArray.ShuffleArray(stimuliBlockSequence);
                        RandomizeArray.ShuffleArray(isiDurations);

                        //reset counter
                        sequenceChunkCounter = 0;
                    }*/

                    //currentStimulus = visualStimuli[stimuliBlockSequence[sequenceChunkCounter]];
                    currentStimulus = visualStimuli[stimuliBlockSequence[trialCounter-1]];

                    //currentIsiDuration = isiDurations[sequenceChunkCounter];
                    currentIsiDuration = isiDurations[trialCounter-1];

                    //increment trial in gait counter
                    currentTrialInGait += 1;

                    break;
                }

        }

        //Debug.Log("currentIsiDuration: " + currentIsiDuration.ToString());

        //write trial start marker
        tempMarkerText =
            "trialStart:" + trialCounter.ToString() + ";" +
            "condition:" + conditions[currentConditionNo] + ";" +
            "isiDuration:" + currentIsiDuration.ToString() + ";" +
            "stimulus:" + currentStimulus;
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);

    }//StartTrial()


    void TriggerVisualStimulus(string side, string color)
    {
        //triggers a visual stimulus by sending a command to the Raspberry PI
        string[] sample = {"led;" + side + ";" + color + ";" + ledBrightness.ToString() + ";" + (stimulusDuration*1000).ToString()};    //convert s to ms
        //ToDo: try catch block!
        visualStimulusStreamOutlet.push_sample(sample);

        //send lsl marker
        tempMarkerText =
            "stimulus:visual" + ";" +
            "side:" + side + ";" +
            "color:" + color + ";" +
            "duration:" + stimulusDuration.ToString();
        marker.Write(tempMarkerText);
        Debug.Log("Triggered Visual Stimulus: " + side + " " + color + " " + currentTime.ToString());

    }


    void TriggerAudioStimulus(string side, string pitch)
    {
        /*
        //play audio stimuli in unity:
        float stereoPan = 0;

        //triggers an audio stimulus
        if (side == "left")
        {
            stereoPan = -1;
            //stereoPan = 1;
        }
        else if (side == "right")
        {
            stereoPan = 1;
        }

        if (pitch == "high")
        {
            audioSource_high.panStereo = stereoPan;
            audioSource_high.volume = audioVolume;
            audioSource_high.Play();
        }
        else if (pitch == "low")
        {
            audioSource_low.panStereo = stereoPan;
            audioSource_low.volume = audioVolume;
            audioSource_low.Play();
        }
        */

        //play audio in raspberry: send sound command to RasPi
        string[] sample = { "audio;" + side + ";" + pitch + ";" + audioVolume.ToString() + ";" + (stimulusDuration * 1000).ToString() };    //convert s to ms
        //ToDo: try catch block!
        visualStimulusStreamOutlet.push_sample(sample);

        //send lsl marker
        tempMarkerText =
            "stimulus:audio" + ";" +
            "side:" + side + ";" +
            "pitch:" + pitch + ";" +
            "duration:" + stimulusDuration.ToString();
        marker.Write(tempMarkerText);
        Debug.Log("Playing Audio Stimulus: " + side + " " + pitch + " " + currentTime.ToString());

    }



    //misc methods
    public int[] CreateTrialSequenceArray(int totalTrials, int[] baseSequence, string[] options)
    {
        // Fill an array with options for the stimuli. 
        // For example: 4 different stimuli options and 100 trials total. The array will be filled using a method 
        // which minimizes repetition in subsequent trials.
        int tempTrialCounter = 0;
        int baseSequenceCounter = 0;
        int[] tempTrialTasks = new int[totalTrials];
        int[] tempSequence = baseSequence;
        RandomizeArray.ShuffleArray(tempSequence);

        while (tempTrialCounter < totalTrials)
        {
            if (baseSequenceCounter >= tempSequence.Length)
            {
                baseSequenceCounter = 0;
                RandomizeArray.ShuffleArray(tempSequence);
            }

            tempTrialTasks[tempTrialCounter] = tempSequence[baseSequenceCounter];

            baseSequenceCounter++;
            tempTrialCounter++;

        }

        
        //Debug: print out the array:
        /*
        Debug.Log("Trial sequence:");
        for (int i=0; i< tempTrialTasks.Length; i++)
        {
            Debug.Log(options[tempTrialTasks[i]]);
        }
        */
        return tempTrialTasks;

    }//CreateTrialSequenceArray()


    public float[] CreateDurationsArray(int arraySize, float durationAverage, float durationVariation)
    {
        //Create an array the size of arraySize with duration values which range from durationAverage-durationVariation to durationAverage+durationVariation.
        //The individual durations will be distributed evenly within this range and the order will be shuffled at the end.

        float[] tempDurations = new float[arraySize];


        //Debug.Log("All durations:");
        for (int i = 0; i < arraySize; i++)
        {
            //the goal here is to get linear distributed values in the range
            tempDurations[i] = i * (durationVariation * 2 / (arraySize - 1)) + durationAverage - durationVariation;
            //Debug.Log(tempDurations[i].ToString());
        }
        //shuffle cue duration order
        RandomizeArray.ShuffleArray(tempDurations);

        return tempDurations;

    }//CreateDurationsArray()


    /*
    bool CheckInsideGait()
    {
        bool isInside = false;
        //checks if the participant is located inside the OptoGait or not
        /*
        if ()
        {
            isInside = true;
        }
        else
        {
            isInside = false;
        }
        *
        return isInside;

    }//checkInsideGait()
    */


    public void SetTriggerPressed(string side)
    {
        //set response time and side
        currentResponseTime = currentTime - currentStimulusTime;
        responseSide = side;

    }


    public bool GetResponseActive()
    {
        return responseActive;
    }



    public void SetCornerWithController()
    {
        SetCorner(cornerCounter);

        cornerCounter += 1;

        if (cornerCounter > 4)
        {
            SetGaitCornersActive(false);
            cornerCounter = 1;

            buttonSetGaitCorners.GetComponent<Button>().interactable = true;
        }

    }


    public bool GetSetGaitCornersActive()
    {
        return setGaitCornersActive;
    }


    public void SetGaitCornersActive(bool active)
    {
        //Debug.Log("Setting SetGaitCornersActive: " + BoolToString(active));

        if (active)
        {
            buttonSetGaitCorners.GetComponent<Button>().interactable = false;
        }

        setGaitCornersActive = active;
    }


    public void SetCorner(int number)
    {
        //this method is executed when pressing a "Set Gait Corner" button in the Calibration menu

        //get position of right controller
        Vector3 tempPosition = controllerRight.transform.position;
        tempPosition.y = 0;     //normalize the height for all corners will help calculations for the OptoGaitCube later because there the height is not relevant

        corners[number - 1] = controllerRight.transform.position;

        print("Set corner" + number.ToString() + " position: " + controllerRight.transform.position.ToString());


        //set flag
        switch (number)
        {
            case 1:
                {
                    cornerOneSet = true;
                    break;
                }
            case 2:
                {
                    cornerTwoSet = true;
                    break;
                }
            case 3:
                {
                    cornerThreeSet = true;
                    break;
                }
            case 4:
                {
                    cornerFourSet = true;
                    break;
                }
        }


        //check if all corners have been set
        if (cornerOneSet && cornerTwoSet && cornerThreeSet && cornerFourSet)
        {
            buttonCreateOptoGaitCube.GetComponent<Button>().interactable = true;
        }

    }


    public void CreateOptoGaitCube()
    {
        //this method is executed when pressing the "Create OptoGait Cube" button

        //find centroid of the 4 corners
        Vector3 centroid = new Vector3();
        centroid = ((corners[0] + corners[1] + corners[2] + corners[3])/corners.Length);

        print("OptoGait cube centroid: " + centroid.ToString());

        //get length and width
        float distanceCornersOneTwo = Vector3.Distance(corners[0], corners[1]);
        float distanceCornersTwoThree = Vector3.Distance(corners[1], corners[2]);
        float distanceCornersThreeFour = Vector3.Distance(corners[2], corners[3]);
        float distanceCornersFourOne = Vector3.Distance(corners[3], corners[0]);

        /*
        print("distance corners 1-2: " + distanceCornersOneTwo.ToString());
        print("distance corners 2-3: " + distanceCornersTwoThree.ToString());
        print("distance corners 3-4: " + distanceCornersThreeFour.ToString());
        print("distance corners 4-1: " + distanceCornersFourOne.ToString());
        */

        //average opposite distances:
        float length = (distanceCornersOneTwo + distanceCornersThreeFour) / 2;
        float width = (distanceCornersTwoThree + distanceCornersFourOne) / 2;

        //print("length: " + length.ToString());
        //print("width: " + width.ToString());


        //calculate angle:
        //algorithm from Timo:
        Vector2 x1 = new Vector2(corners[0].x, corners[0].z);
        Vector2 x2 = new Vector2(corners[1].x, corners[1].z);
        //print("x1:" + x1.ToString() + " x2:" + x2.ToString());

        Vector2 vec1 = x2 - x1;
        Vector2 vec2 = new Vector2(0, 1);
        //print("vec1:" + vec1.ToString() + " vec2:" + vec2.ToString());

        float angle = Vector2.SignedAngle(vec1, vec2);
        //print("angle: " + angle.ToString());

        Vector3 axis = Vector3.zero;


        //create and position object
        optoGait.transform.position = new Vector3(centroid.x, 0.1f, centroid.z);    //move to ground
        optoGait.transform.localScale = new Vector3(width, 0.2f, length);     //height is not relevant
        optoGait.transform.eulerAngles = new Vector3(0f, angle, 0f);


        //set flag
        gaitPositionsSet = true;

    }


    public void IncrementInsideGaitCounter()
    {
        controllerInsideGaitCounter += 1;

        //lsl marker
        marker.Write("foot inside gait:" + controllerInsideGaitCounter.ToString());
        print("foot inside gait: " + controllerInsideGaitCounter.ToString());

        //check if new gait pass:
        //if (controllerInsideGaitCounter == 2)
        if (controllerInsideGaitCounter == 1)   //the first foot
        {
            //set insideGait flag
            insideGait = true;

            //increment gait pass counter
            gaitPassCounter += 1;

            //reset trial in gait counter
            currentTrialInGait = 0;

            //reset gait trials reached
            maxGaitTrialsReached = false;

            //lsl marker
            marker.Write("new gait pass:" + gaitPassCounter.ToString());
            print("new gait pass:" + gaitPassCounter.ToString());

            //change color of optogait object
            optoGait.GetComponent<MeshRenderer>().material.color = Color.green;

        }

    }

    public void DecrementInsideGaitCounter()
    {
        controllerInsideGaitCounter -= 1;

        if(controllerInsideGaitCounter == 0)
        {
            //set insideGait flag
            insideGait = false;

            //change color of optogait object
            optoGait.GetComponent<MeshRenderer>().material.color = Color.yellow;
        }

        //lsl marker
        marker.Write("controller inside Gait:" + controllerInsideGaitCounter.ToString());
        print("controller inside gait: " + controllerInsideGaitCounter.ToString());

    }


    private void SetDesktopInfoTexts(string condition, string runNo, string trialNo, string trialNoInGait, string gaitPassNo, string time)
    {
        textCondition.GetComponent<Text>().text = condition;
        textConditionRunNo.GetComponent<Text>().text = runNo;
        textTrialNo.GetComponent<Text>().text = trialNo;
        textTrialInGaitNo.GetComponent<Text>().text = trialNoInGait;
        textGaitPassNo.GetComponent<Text>().text = gaitPassNo;
        textTime.GetComponent<Text>().text = time;

    }


    public void VibrateController(string side)
    {
        if (side.Contains("left"))
        {
            SteamVR_Controller.Input((int)controllerLeft.GetComponent<SteamVR_TrackedController>().controllerIndex).TriggerHapticPulse(3999);
        }
        else if (side.Contains("right"))
        {
            SteamVR_Controller.Input((int)controllerRight.GetComponent<SteamVR_TrackedController>().controllerIndex).TriggerHapticPulse(3999);
        }
    }


    static string BoolToString(bool b)
    {
        return b ? "true" : "false";
    }


    private bool ResolveRasPiStream()
    {
        print("resolving rapsi answers stream...");
        liblsl.StreamInfo[] resolvedStreams = liblsl.resolve_stream("name", "HearingImpaired_RasPi_Answers", 1, 10);

        if (resolvedStreams.Length > 0)
        {
            rasPiStreamInlet = new liblsl.StreamInlet(resolvedStreams[0]);
            print("stream found");
            return true;
        }
        else
        {
            print("Stream not found");
            return false;
        }
    }


    public void ConnectRasPi()
    {
        //disable button
        buttonConnectRasPi.GetComponent<Button>().interactable = false;

        print("Connection test with RasPi:");
        print("Trying to resolve RasPi stream...");

        //try to resolve the RasPi stream
        if (ResolveRasPiStream())
        {
            print("RasPi stream found.");

            //if RasPi stream found -> check if RasPi answers to connection check
            print("Sending test command to RasPi...");
            string[] sample = {"test connection"};
            visualStimulusStreamOutlet.push_sample(sample);

            print("Waiting for answer from RasPi...");
            string[] sampleReceived = new string[1];
            rasPiStreamInlet.pull_sample(sampleReceived, 10);

            if (sampleReceived[0].Length > 0)
            {
                //for (int i = 0; i < sampleReceived[0].Length; i++)
                //{
                    print("sampleReceived[0]: " + sampleReceived[0]);
                //}
                

                if (sampleReceived[0].Contains("connection answer"))
                {
                    print("RasPi answered. Connection successful.");

                    //set text and set color to green
                    rasPiNotConnectedText.GetComponent<Text>().text = rasPiConnected;
                    rasPiNotConnectedText.GetComponent<Text>().color = Color.green;
                }
                else
                {
                    print("Wrong answer from RasPi. Connection failed.");
                    //set text and set color to red
                    rasPiNotConnectedText.GetComponent<Text>().text = rasPiNotConnected;
                    rasPiNotConnectedText.GetComponent<Text>().color = Color.red;
                }
            }
            else
            {
                print("No answer from RasPi. Connection failed.");
                //set text and set color to red
                rasPiNotConnectedText.GetComponent<Text>().text = rasPiNotConnected;
                rasPiNotConnectedText.GetComponent<Text>().color = Color.red;
            }
            
        }
        else
        {
            print("RasPi stream not found. Connection failed.");
            //set text and set color to red
            rasPiNotConnectedText.GetComponent<Text>().text = rasPiNotConnected;
            rasPiNotConnectedText.GetComponent<Text>().color = Color.red;
        }

        //enable button
        buttonConnectRasPi.GetComponent<Button>().interactable = true;

    }


    public void CheckSequenceInput()
    {
        //Debug.Log("inputSequence: " +  inputSequence.GetComponent<Dropdown>().options[inputSequence.GetComponent<Dropdown>().value].text);
        if (inputSequence.GetComponent<Dropdown>().options[inputSequence.GetComponent<Dropdown>().value].text.Equals("?"))
        {
            buttonExpSequence.GetComponent<Button>().interactable = false;
        }
        else
        {
            buttonExpSequence.GetComponent<Button>().interactable = true;
        }

    }


    public void ConnectOptoGait()
    {
        optoGaitConnectionText.GetComponent<Text>().color = Color.red;

        if (optoApiClient.InitSocket(optoApiHostIP, optoApiHostPort))
        {
            if (optoApiClient.CheckConnection(optoApiHostIP, optoApiHostPort))
            {
                //connected to OptoGait
                optoGaitConnectionText.GetComponent<Text>().color = Color.green;
                optoGaitConnected = true;

            }
        }

    }


    public void DisconnectOptoGait()
    {
        if (optoApiClient.CheckConnection(optoApiHostIP, optoApiHostPort))
        {
            //disconnect the OptoGait server
            if (optoApiClient.CloseSocket())
            {
                optoGaitConnectionText.GetComponent<Text>().color = Color.red;
                optoGaitConnected = false;

            }

        }
    }



}//class