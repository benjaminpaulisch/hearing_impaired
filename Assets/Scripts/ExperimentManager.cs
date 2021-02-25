using UnityEngine;
using UnityEngine.UI;
using Assets.LSL4Unity.Scripts; // reference the LSL4Unity namespace to get access to all classes
using System.Collections.Generic;

public class ExperimentManager : MonoBehaviour
{
    //##### Inspector Interface #####
    [Header("General Config")]
    public float isiDurationAvg = 1;                //1s ISI duration on average
    public float isiDurationVariation = 0.5f;       //0.5s variation (so the ISI duration range is betweeen 0.5s and 1.5s)
    public float stimulusDuration = 0.01f;          //100ms stimulus duration
    public float responseTimeMax = 1.9f;            //1.5s max possible response time

    [Header("Experiment specific")]
    public int gaitPassesPerBlock = 35;             
    public int trialsSittingPerBlock = 100;
    public int trialsWalkingChunkMultiplier = 10;   //will be multiplied with the amount of different stimuli to get a chunk size for the walking conditions

    [Header("Training specific")]
    public int trialsPerCondTraining = 5;

    [Header("Baseline specific")]
    public int baselineDuration = 300;              //5 minutes

    [Header("Misc")]
    public LSLMarkerStream marker;

    //##########


    // General vars
    private string[] conditions = new string[7] { "ST_walking", "ST_audio", "DT_audio", "ST_visual",  "DT_visual", "BL_sitting", "BL_walking" };
    private int[,] conditionSequences = new int[12, 5] {        //the 12 sequences from the ethics document
        { 0, 1, 2, 3, 4 },      //Sequence 1
        { 0, 1, 4, 3, 2 },      //Sequence 2
        { 0, 3, 2, 1, 4 },      //Sequence 3
        { 0, 3, 4, 1, 2 },      //Sequence 4
        { 2, 1, 0, 3, 4 },      //Sequence 5
        { 2, 1, 4, 3, 0 },      //Sequence 6
        { 2, 3, 0, 1, 4 },      //Sequence 7
        { 2, 3, 4, 1, 0 },      //Sequence 8
        { 4, 1, 2, 3, 0 },      //Sequence 9
        { 4, 1, 0, 3, 2 },      //Sequence 10
        { 4, 3, 2, 1, 0 },      //Sequence 11
        { 4, 3, 0, 1, 2 }      //Sequence 12
        };
    private int currentConditionNo;

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
    private int insideGaitCounter = 0;              //used to store the amount of controllers inside OptoGait object collider

    private float currentTime = 0;                  //current time value during a trial
    private float currentStimulusTime = 0;          //the time value when the stimulus was shown during the current trial
    private float currentResponseTime = 0;          //calculated response time (duration)

    private string participantID;
    private int participantAge;
    private string participantGroup;
    private string participantGender;

    private string tempMarkerText;

    private Vector3[] corners = new Vector3[4];     //used to store the 4 corner positions of the OptoGait you can put in in the calibration menu


    // Experiment specific
    private int stWalkingRunNo = 0;
    private int stAudioRunNo = 0;
    private int stVisualRunNo = 0;
    private int dtAudioRunNo = 0;
    private int dtVisualRunNo = 0;
    private float[] isiDurations;
    private int trialCounter = 0;
    private int sequenceChunkCounter = 0;       //used only in walking conditions for isi and trials sequences
    private float currentIsiDuration;           //stores individual ISI duration of the current trial
    private string responseSide = "";
    private bool responseActive = false;
    //public static bool responseActive = false;

    // Training specific
    private int trainingRunNo = 0;

    // Baseline specific
    private int baselineSitRunNo = 0;
    private int baselineWalkRunNo = 0;


    // Programm logic handler
    private int programStatus = 0;                  //indicating the current status of the program (main menu, training, experiment etc)
    private bool idSet = false;
    private bool ageSet = false;
    private bool groupSet = false;
    private bool genderSet = false;
    private bool gaitPositionsSet = false;

    // Experiment logic handler
    private bool experimentStarted = false;
    private bool expInitRun = false;
    private bool experimentEnd = false;

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
    private GameObject mainMenuCanvas, configMenuCanvas, calibrationMenuCanvas, desktopInfoCanvas,
        buttonTraining, buttonBaselineWalking, buttonBaselineSitting, buttonSittingVisual, buttonSittingAudio, buttonWalkingST, buttonWalkingVisual, buttonWalkingAudio,
        inputParticipantID, inputParticipantAge, inputParticipantGroup, inputParticipantGender,
        textCondition, textConditionRunNo, textTrialNo, textGaitPassNo, textTime,
        optoGait
        ;

    public AudioSource audioSource_high, audioSource_low;

    public GameObject controllerLeft, controllerRight;




    void Start()
    {
        // Start is called before the first frame update

        // Finding the game objects:
        marker = FindObjectOfType<LSLMarkerStream>();
        mainMenuCanvas = GameObject.Find("MainMenuCanvas");
        buttonTraining = GameObject.Find("ButtonTraining");
        buttonBaselineWalking = GameObject.Find("Table");
        buttonBaselineSitting = GameObject.Find("ButtonBaselineWalking");
        buttonSittingVisual = GameObject.Find("ButtonSittingVisual");
        buttonSittingAudio = GameObject.Find("ButtonSittingAudio");
        buttonWalkingST = GameObject.Find("ButtonWalkingST");
        buttonWalkingVisual = GameObject.Find("ButtonWalkingVisual");
        buttonWalkingAudio = GameObject.Find("ButtonWalkingAudio");
        configMenuCanvas = GameObject.Find("ConfigMenuCanvas");
        calibrationMenuCanvas = GameObject.Find("CalibrationMenuCanvas");
        inputParticipantID = GameObject.Find("InputParticipantID");
        inputParticipantAge = GameObject.Find("InputParticipantAge");
        inputParticipantGroup = GameObject.Find("DropdownParticipantGroup");
        inputParticipantGender = GameObject.Find("DropdownParticipantGender");
        desktopInfoCanvas = GameObject.Find("DesktopInfoCanvas");
        textCondition = GameObject.Find("TextCondition");
        textConditionRunNo = GameObject.Find("TextConditionRunNo");
        textTrialNo = GameObject.Find("TextTrialNo");
        textGaitPassNo = GameObject.Find("TextGaitPassNo");
        textTime = GameObject.Find("TextTime");
        controllerLeft = GameObject.Find("Controller (left)");
        controllerRight = GameObject.Find("Controller (right)");
        optoGait = GameObject.Find("OptoGait");

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
                Debug.Log("Controller (left) is null");
                controllerLeft = GameObject.Find("Controller (left)");
            }
            if (controllerRight == null)
            {
                Debug.Log("Controller (right) is null");
                controllerRight = GameObject.Find("Controller (right)");
            }
            

            //Debug.Log("GetJoystickNames: " + UnityEngine.Input.GetJoystickNames());

            //exp status check
            switch (programStatus)
            {
                case 0: //main menu
                    {
                        break;
                    }

                case 1: //configuration
                    {
                        break;
                    }

                case 2: //calibration
                    {
                        break;
                    }

                case 3: //training
                    {
                        if (Input.GetKeyDown("escape"))
                        {
                            marker.Write("training:abort");
                            Debug.Log("training:abort");

                            trainingStarted = false;

                            //go to main menu
                            StartMainMenu();
                        }
                        else if (!trainingInitRun)
                        {
                            InitTraining();
                        }
                        else
                        {

                        }

                        break;
                    }

                case 4: //experiment
                    {
                        //check for abort by pressing the escape key
                        if (Input.GetKeyDown("escape"))
                        {
                            marker.Write("experiment:abort");
                            Debug.Log("experiment:abort");

                            experimentStarted = false;

                            //go to main menu
                            StartMainMenu();
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
                            else if (currentConditionNo == 1 || currentConditionNo == 3)
                            {
                                //single task sitting conditions
                                InitExperimentSitting();
                                experimentStarted = true;
                            }
                            else if (currentConditionNo == 2 || currentConditionNo == 4)
                            {
                                //dual task walking conditions
                                InitExperimentWalking();
                            }
                        }
                        else
                        {
                            //Run experiment
                            RunExperiment();
                        }

                        break;
                    }

                case 5: //baseline
                    {
                        //check for abort by pressing the escape key
                        if (Input.GetKeyDown("escape"))
                        {
                            marker.Write("baseline:abort");
                            Debug.Log("baseline:abort");
                            
                            baselineStarted = false;

                            //go to main menu
                            StartMainMenu();
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

                                    //Go back to main menu
                                    StartMainMenu();
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

                                    SetDesktopInfoTexts(conditions[currentConditionNo], tempNo.ToString(), "-", "-", string.Format("{0}:{1:00}", (int)currentTime / 60, (int)currentTime % 60));
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

        expInitRun = false;
        baselineInitRun = false;
        trainingInitRun = false;

        //activate/deactivate GameObjects
        mainMenuCanvas.SetActive(true);
        configMenuCanvas.SetActive(false);
        calibrationMenuCanvas.SetActive(false);
        desktopInfoCanvas.SetActive(false);


    }//StartMainMenu()


    public void StartConfiguration()
    {
        //This method is used for the "Configuration" button on the main menu. WHen the button is pressed this method is executed.
        Debug.Log("Starting Configuration");
        programStatus = 1;

        //activate/deactivate GameObjects
        mainMenuCanvas.SetActive(false);
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
        /*
        //participantGroup
        if (!inputParticipantGroup.GetComponent<Dropdown>().options[inputParticipantGroup.GetComponent<Dropdown>().value].text.Equals("?"))
        {
            groupSet = true;
            participantGroup = inputParticipantGroup.GetComponent<Dropdown>().options[inputParticipantGroup.GetComponent<Dropdown>().value].text;
        }
        else
            genderSet = false;
        */

        //participantGender
        if (!inputParticipantGender.GetComponent<Dropdown>().options[inputParticipantGender.GetComponent<Dropdown>().value].text.Equals("?"))
        {
            genderSet = true;
            participantGender = inputParticipantGender.GetComponent<Dropdown>().options[inputParticipantGender.GetComponent<Dropdown>().value].text;
        }
        else
            genderSet = false;
        
        
        /*
        Debug.Log("participantID: " + participantID + " InputField.text: " + inputParticipantID.GetComponent<InputField>().text);
        Debug.Log("participantAge: " + participantAge.ToString() + " InputField.text: " + inputParticipantAge.GetComponent<InputField>().text);
        Debug.Log("participantGroup: " + participantGroup + " InputField.text: " + inputParticipantGroup.GetComponent<Dropdown>().options[inputParticipantGroup.GetComponent<Dropdown>().value].text);
        Debug.Log("participantGender: " + participantGender + " InputField.text: " + inputParticipantGender.GetComponent<Dropdown>().options[inputParticipantGender.GetComponent<Dropdown>().value].text);
        */

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
        configMenuCanvas.SetActive(false);
        calibrationMenuCanvas.SetActive(true);
        desktopInfoCanvas.SetActive(false);


    }//StartCalibration()


    public void StartTraining()
    {
        //This method is used for the "Start Training" button on the main menu. When the button is pressed this method is executed.
        marker.Write("Main menu: Start Training button pressed");
        Debug.Log("Starting Training");
        programStatus = 3;

        //activate/deactivate GameObjects
        mainMenuCanvas.SetActive(false);
        configMenuCanvas.SetActive(false);
        calibrationMenuCanvas.SetActive(false);
        desktopInfoCanvas.SetActive(true);


    }//StartTraining()


    public void StartBaseline(int conditionNo)
    {
        //This method is used for all "Start Baseline" buttons in the main menu. If one of these buttons is pressed this method is executed.
        marker.Write("Main menu: Start " + conditions[conditionNo] + " button pressed");
        Debug.Log("Starting " + conditions[conditionNo]);
        programStatus = 5;

        currentConditionNo = conditionNo;

        //activate/deactivate GameObjects
        mainMenuCanvas.SetActive(false);
        configMenuCanvas.SetActive(false);
        calibrationMenuCanvas.SetActive(false);
        desktopInfoCanvas.SetActive(true);

    }


    public void StartExperiment(int conditionNo)
    {
        //This method is used for all "Start Block" buttons in the main menu. If one of these buttons is pressed this method is executed.
        marker.Write("Main menu: Start Experiment button pressed");
        Debug.Log("Starting Experiment");
        programStatus = 4;

        currentConditionNo = conditionNo;

        //activate/deactivate GameObjects
        mainMenuCanvas.SetActive(false);
        configMenuCanvas.SetActive(false);
        calibrationMenuCanvas.SetActive(false);
        desktopInfoCanvas.SetActive(true);

    }//StartExperiment()



    //Init methods
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
            stimuliBlockSequence = CreateTrialSequenceArray(trialsSittingPerBlock, stimuliBaseSequence, visualStimuli);

            //increment condition counter
            stVisualRunNo += 1;
            currentConditionCounter = stVisualRunNo;
        }
        else
        {
            //create trial sequence for the block
            stimuliBlockSequence = CreateTrialSequenceArray(trialsSittingPerBlock, stimuliBaseSequence, audioStimuli);

            //increment condition counter
            stAudioRunNo += 1;
            currentConditionCounter = stAudioRunNo;
        }

        //Create isi durations for the block
        isiDurations = CreateDurationsArray(trialsSittingPerBlock, isiDurationAvg, isiDurationVariation);


        //write experiment start marker
        tempMarkerText =
            "experiment:start;" +
            "condition:" + conditions[currentConditionNo] + ";" +
            "runNo:" + currentConditionCounter.ToString() + ";" +
            "trialsTotal:" + trialsSittingPerBlock.ToString() + ";" +
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
            "participantGender:" + participantGender;
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
            stimuliBlockSequence = CreateTrialSequenceArray(visualStimuli.Length * trialsWalkingChunkMultiplier, stimuliBaseSequence, visualStimuli);

            //increment condition counter
            dtVisualRunNo += 1;
            currentConditionCounter = dtVisualRunNo;
        }
        else
        {
            //create trial sequence for the block
            stimuliBlockSequence = CreateTrialSequenceArray(audioStimuli.Length * trialsWalkingChunkMultiplier, stimuliBaseSequence, audioStimuli);

            //increment condition counter
            dtAudioRunNo += 1;
            currentConditionCounter = dtAudioRunNo;
        }

        //Create isi durations for the block
        isiDurations = CreateDurationsArray(stimuliBaseSequence.Length * trialsWalkingChunkMultiplier, isiDurationAvg, isiDurationVariation);


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
            "participantGender:" + participantGender;
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


    void InitBaseline()
    {
        //supposed to only run once in the beginning of a baseline

        baselineEnd = false;
        currentTime = 0;

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
            "participantGender:" + participantGender;
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
        SetDesktopInfoTexts(conditions[currentConditionNo], currentConditionCounter.ToString(), "-", "-", string.Format("{0}:{1:00}", (int)currentTime / 60, (int)currentTime % 60));


        baselineInitRun = true;

    }//InitBaseline()


    void InitTraining()
    {
        //supposed to only run once in the beginning of a training run

        trainingEnd = false;
        trialCounter = 0;
        trainingRunNo += 1;

        /* ToDo
        //write training start marker
        tempMarkerText =
            "training:start;" +
            "condition:" + conditions[currentConditionNo] + ";" +
            "runNo:" + stWalkingRunNo.ToString() + ";" +
            "gaitPasses:" + gaitPassesPerBlock.ToString();

        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);

        //write participant info (from configuration menu)
        tempMarkerText =
            "participantID:" + participantID + ";" +
            "participantAge:" + participantAge.ToString() + ";" +
            "participantGroup:" + participantGroup + ";" +
            "participantGender:" + participantGender;
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
        */


        //set desktop info texts
        SetDesktopInfoTexts(conditions[currentConditionNo], trainingRunNo.ToString(), "", "-", "-");


        expInitRun = true;

        //start first trial
        StartTrial();

    }


    void InitWalkingST()
    {
        //supposed to only run once in the beginning of an experiment run

        experimentEnd = false;
        //trialCounter = 0;
        gaitPassCounter = 0;


        stWalkingRunNo += 1;

        //write experiment start marker
        tempMarkerText =
            "experiment:start;" +
            "condition:" + conditions[currentConditionNo] + ";" +
            "runNo:" + stWalkingRunNo.ToString() + ";" +
            "gaitPasses:" + gaitPassesPerBlock.ToString();
           
        marker.Write(tempMarkerText);
        Debug.Log(tempMarkerText);

        //write participant info (from configuration menu)
        tempMarkerText =
            "participantID:" + participantID + ";" +
            "participantAge:" + participantAge.ToString() + ";" +
            "participantGroup:" + participantGroup + ";" +
            "participantGender:" + participantGender;
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
        SetDesktopInfoTexts(conditions[currentConditionNo], stWalkingRunNo.ToString(), "-", "", "-");


        expInitRun = true;

    }//InitWalkingST()


    void RunExperiment()
    {
        //controls all trials during an experiment run

        //Debug.Log("RunExperiment()");
        //Debug.Log("experimentStarted: " + BoolToString(experimentStarted));


        // ### For all walking conditions: check if inside gait first and only start trial if new inside
        //if (currentConditionNo == 0 || currentConditionNo == 2 || currentConditionNo == 4)
        if (currentConditionNo == 2 || currentConditionNo == 4) //not condition ST_walking!
        {

            if (insideGaitCounter == 2)
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
                //if not inside gait

                //abort current trial if it's a new outside gait
                if (experimentStarted)
                {
                    //abort current trial
                    experimentStarted = false;

                    //lsl marker
                    marker.Write("trialAbort:" + trialCounter.ToString());
                    Debug.Log("trial aborted! TrialNo:" + trialCounter.ToString());

                    //go to next trial
                    NextTrial();

                }
            }

        }


        if (experimentStarted)
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
                }

                //update desktop info texts
                SetDesktopInfoTexts(conditions[currentConditionNo], stWalkingRunNo.ToString(), "-", gaitPassCounter.ToString(), "-");

            } 
            else if (currentConditionNo == 2 || currentConditionNo == 4)
            {
                //dual task walking conditions
                if (gaitPassCounter < gaitPassesPerBlock)
                {
                    RunTrial();
                }

                //update desktop info texts
                int tempRunNo;
                if (currentConditionNo == 2)
                {
                    tempRunNo = dtAudioRunNo;
                }
                else
                {
                    tempRunNo = dtVisualRunNo;
                }

                SetDesktopInfoTexts(conditions[currentConditionNo], tempRunNo.ToString(), trialCounter.ToString(), gaitPassCounter.ToString(), "-");

            }
            else if (currentConditionNo == 1 || currentConditionNo == 3 )      
            {
                //single task sitting conditions
                if (trialCounter < trialsSittingPerBlock && !experimentEnd)
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

                SetDesktopInfoTexts(conditions[currentConditionNo], tempRunNo.ToString(), trialCounter.ToString(), "-", "-");

            }

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
                    marker.Write("experiment:end");
                    Debug.Log("experiment:end");
                }

                //activate experiment end text
                //end.SetActive(true);

                experimentStarted = false;

                //go to main menu
                StartMainMenu();
            }

        }//if experimentStarted

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
            if (currentConditionNo == 1 || currentConditionNo == 2)
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
            else if (currentConditionNo == 3 || currentConditionNo == 4)
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

                TriggerAudioStimulus(stimulusSide, stimulusColor);

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
                tempMarkerText = 
                    "response:" + responseSide + ";" +
                    "duration:" + currentResponseTime;
                marker.Write(tempMarkerText);
                Debug.Log(tempMarkerText);

                //go to next trial
                NextTrial();

            }

        }

        if (currentTime > currentIsiDuration + stimulusDuration + responseTimeMax)
        {
            //response time over
            marker.Write("response time over");
            Debug.Log("response time over. " + currentTime.ToString());

            //go to next trial
            NextTrial();

        }

    }//RunTrial()


    void NextTrial()
    {
        //controls transition to the next trial or end of the block

        //send trial end marker
        marker.Write("trialEnd:" + trialCounter.ToString());
        Debug.Log("trialEnd:" + trialCounter.ToString());

        trialCounter += 1;

        //in walking conditions also increment sequence counter
        if (currentConditionNo == 2 || currentConditionNo == 4)
        {
            sequenceChunkCounter += 1;
        }


        //reset vars
        currentTime = 0.0f;
        responseActive = false;
        print("responseActive: false");

        //check if block end condition is fullfilled
        if (currentConditionNo == 2 || currentConditionNo == 4)
        {
            //dual task walking conditions
            if (gaitPassCounter > gaitPassesPerBlock)
            {

                //set flag for experiment end and don't start another trial
                experimentEnd = true;
            }
            else
            {
                //start next trial if exp is running (if NOT we could be ouside gait and don't want to start a new trial!)
                if (experimentStarted)
                {
                    StartTrial();
                }
            }

        }
        else if (currentConditionNo == 1 || currentConditionNo == 3)
        {
            //single task sitting conditions
            if (trialCounter == trialsSittingPerBlock)
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
                    currentStimulus = audioStimuli[stimuliBlockSequence[trialCounter]];

                    currentIsiDuration = isiDurations[trialCounter];

                    break;
                }
            case 2: //DT_audio
                {
                    //check if sequence is finished -> start new sequence
                    if (sequenceChunkCounter >= stimuliBlockSequence.Length)
                    {
                        //reshuffle sequences
                        RandomizeArray.ShuffleArray(stimuliBlockSequence);
                        RandomizeArray.ShuffleArray(isiDurations);

                        //reset counter
                        sequenceChunkCounter = 0;
                    }

                    currentStimulus = audioStimuli[stimuliBlockSequence[sequenceChunkCounter]];

                    currentIsiDuration = isiDurations[sequenceChunkCounter];

                    break;
                }
            case 3: //ST_visual
                {
                    currentStimulus = visualStimuli[stimuliBlockSequence[trialCounter]];

                    currentIsiDuration = isiDurations[trialCounter];

                    break;
                }
            case 4: //DT_visual
                {
                    //check if sequence is finished -> start new sequence
                    if (sequenceChunkCounter >= stimuliBlockSequence.Length)
                    {
                        //reshuffle sequence
                        RandomizeArray.ShuffleArray(stimuliBlockSequence);
                        RandomizeArray.ShuffleArray(isiDurations);

                        //reset counter
                        sequenceChunkCounter = 0;
                    }

                    currentStimulus = visualStimuli[stimuliBlockSequence[sequenceChunkCounter]];

                    currentIsiDuration = isiDurations[sequenceChunkCounter];

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
        //ToDo

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
        float stereoPan = 0;

        //triggers an audio stimulus
        if (side == "left")
        {
            stereoPan = -1;
        }
        else if (side == "right")
        {
            stereoPan = 1;
        }

        if (pitch == "high")
        {
            audioSource_high.panStereo = stereoPan;
            audioSource_high.Play();
        }
        else if (pitch == "low")
        {
            audioSource_low.panStereo = stereoPan;
            audioSource_low.Play();
        }

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

        /*
        //Debug: print out the array:
        Debug.Log("Trial sequence:");
        for (int i=0; i< tempTrialTasks.Length; i++)
        {
            Debug.Log(options[tempTrialTasks[i]]);
        }*/

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

   
    public void SetCorner(int number)
    {
        //this method is executed when pressing a "Set Gait Corner" button in the Calibration menu

        //get position of right controller
        Vector3 tempPosition = controllerRight.transform.position;
        tempPosition.y = 0;     //normalize the height for all corners will help calculations for the OptoGaitCube later because there the height is not relevant

        corners[number - 1] = controllerRight.transform.position;

        print("Set corner" + number.ToString() + " position: " + controllerRight.transform.position.ToString());

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
        //GameObject optoGaitCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        optoGait.transform.position = new Vector3(centroid.x, 0.1f, centroid.z);    //move to ground
        optoGait.transform.localScale = new Vector3(width, 0.2f, length);     //height is not relevant
        optoGait.transform.eulerAngles = new Vector3(0f, angle, 0f);

        //change color
        //optoGaitCube.GetComponent<MeshRenderer>().material.color = Color.yellow;

        //resize and position collider
        //optoGaitCube.GetComponent<BoxCollider>().size = new Vector3(1, 10, 1);
        //optoGaitCube.GetComponent<BoxCollider>().center = new Vector3(0, 4.5f, 0);

    }


    public void IncrementInsideGaitCounter()
    {
        insideGaitCounter += 1;

        //lsl marker
        marker.Write("incremented insideGaitCounter to " + insideGaitCounter.ToString());
        print("incremented insideGaitCounter to " + insideGaitCounter.ToString());

        //check if new gait pass:
        if (insideGaitCounter == 2)
        {
            //increment gait pass counter
            gaitPassCounter += 1;
            
            //lsl marker
            marker.Write("incremented gaitPassCounter to " + gaitPassCounter.ToString());
            print("incremented gaitPassCounter to " + gaitPassCounter.ToString());

            //change color of optogait object
            optoGait.GetComponent<MeshRenderer>().material.color = Color.green;

        }

    }

    public void DecrementInsideGaitCounter()
    {
        insideGaitCounter -= 1;

        //lsl marker
        marker.Write("decremented insideGaitCounter to " + insideGaitCounter.ToString());
        print("decremented insideGaitCounter to " + insideGaitCounter.ToString());

        //change color of optogait object
        optoGait.GetComponent<MeshRenderer>().material.color = Color.yellow;

    }


    private void SetDesktopInfoTexts(string condition, string runNo, string trialNo, string gaitPassNo, string time)
    {
        textCondition.GetComponent<Text>().text = condition;
        textConditionRunNo.GetComponent<Text>().text = runNo;
        textTrialNo.GetComponent<Text>().text = trialNo;
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

}//class