using UnityEngine;
using UnityEngine.UI;
using Assets.LSL4Unity.Scripts; // reference the LSL4Unity namespace to get access to all classes


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

    [Header("Training specific")]
    public int trialsPerCondTraining = 5;

    [Header("Baseline specific")]
    public int baselineDuration = 300;              //5 minutes

    [Header("Misc")]
    public LSLMarkerStream marker;

    //##########


    // General vars
    private string[] conditions = new string[7] { "ST_walking", "ST_audio", "DT_audio", "ST_visual",  "DT_visual", "Baseline_sitting", "Baseline_walking" };
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

    private float currentTime = 0;                  //current time value during a trial
    private float currentStimulusTime = 0;          //the time value when the stimulus was shown during the current trial
    private float currentResponseTime = 0;          //calculated response time (duration)

    private string participantID;
    private int participantAge;
    private string participantGroup;
    private string participantGender;

    private string tempMarkerText;


    // Experiment specific
    private int stWalkingRunNo = 0;
    private int stAudioRunNo = 0;
    private int stVisualRunNo = 0;
    private int dtAudioRunNo = 0;
    private int dtVisualRunNo = 0;
    private float[] isiDurations;
    private int trialCounter = 0;
    private float currentIsiDuration;         //stores individual ISI duration of the current trial

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
    private GameObject mainMenuCanvas, configMenuCanvas, calibrationMenuCanvas,
        buttonTraining, buttonBaselineWalking, buttonBaselineSitting, buttonSittingVisual, buttonSittingAudio, buttonWalkingST, buttonWalkingVisual, buttonWalkingAudio,
        inputParticipantID, inputParticipantAge, inputParticipantGroup, inputParticipantGender
        ;




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

        // start the Main Menu:
        StartMainMenu();

    }//start()


    void Update()
    {
        // Update is called once per frame
        try
        {
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
                            }
                            else if (currentConditionNo == 1 || currentConditionNo == 3)
                            {
                                //single task sitting conditions
                                InitExperimentSitting();
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
        configMenuCanvas.SetActive(false);
        calibrationMenuCanvas.SetActive(false);


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

    }//StartExperiment()



    //Init methods
    void InitExperimentSitting()
    {
        //supposed to only run once in the beginning of an experiment run

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

        expInitRun = true;

    }//InitExperimentSitting()


    void InitExperimentWalking()
    {
        //supposed to only run once in the beginning of an experiment run

        experimentEnd = false;
        trialCounter = 0;
        gaitPassCounter = 0;
        int currentConditionCounter;

        if (conditions[currentConditionNo].Contains("visual"))
        {
            //create trial sequence for the block
            //ToDo

            //increment condition counter
            dtVisualRunNo += 1;
            currentConditionCounter = dtVisualRunNo;
        }
        else
        {
            //create trial sequence for the block
            //ToDo

            //increment condition counter
            dtAudioRunNo += 1;
            currentConditionCounter = dtAudioRunNo;
        }

        //Create isi durations for the block
        //ToDo


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


        expInitRun = true;

    }//InitExperimentWalking()


    void InitBaseline()
    {
        //supposed to only run once in the beginning of a baseline
        baselineEnd = false;

        if (currentConditionNo == 6)
        {
            baselineSitRunNo += 1;

            //write baseline start marker
            tempMarkerText =
            "baseline:start;" +
            "condition:" + conditions[currentConditionNo] + ";" +
            "runNo:" + baselineSitRunNo.ToString() + ";" +
            "duration:" + baselineDuration.ToString();

        }
        else if (currentConditionNo == 7)
        {
            baselineWalkRunNo += 1;

            //write baseline start marker
            tempMarkerText =
            "baseline:start;" +
            "condition:" + conditions[currentConditionNo] + ";" +
            "runNo:" + baselineWalkRunNo.ToString() + ";" +
            "duration:" + baselineDuration.ToString();

        }

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

        expInitRun = true;

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

        expInitRun = true;

    }//InitWalkingST()


    void RunExperiment()
    {
        //controls all trials during an experiment run

        if (experimentStarted)
        {
            //update currentTime (adding the time taken to render last frame)
            currentTime += Time.deltaTime;

            // run all trials
            if (currentConditionNo == 0)
            {
                //single task walking
                if (gaitPassCounter < gaitPassesPerBlock)
                {
                    //check if participant is inside OptoGait
                    if (checkInsideGait())
                    {
                        //extra method or RunTrial()?
                    }
                }
            } 
           else if (currentConditionNo == 2 || currentConditionNo == 4)
            {
                //dual task walking conditions
                if (gaitPassCounter < gaitPassesPerBlock)
                {
                    //check if participant is inside OptoGait
                    if (checkInsideGait())
                    {
                        RunTrial();
                    }
                }
            }
            else if (currentConditionNo == 1 || currentConditionNo == 3 )      
            {
                //single task sitting conditions
                if (trialCounter < trialsSittingPerBlock && !experimentEnd)
                {
                    RunTrial();
                }
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

        //Start of trial: ISI
        if (currentTime <= currentIsiDuration)
        {
            if (!isiStarted)
            {
                isiStarted = true;
                marker.Write("ISI started");
                Debug.Log("ISI started: " + currentTime.ToString());
            }
        }

        //After ISI -> trigger a stimulus (audio/visual)
        else if (currentTime <= currentIsiDuration + stimulusDuration)
        {
            if (!stimulusShown) {
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
        }

        //After stimulus -> wait and check for response
        else if (currentTime <= currentIsiDuration + stimulusDuration + responseTimeMax)
        {
            //check for response

            //if response -> Go to next trial
            NextTrial();

        }
        else if (currentTime > currentIsiDuration + stimulusDuration + responseTimeMax)
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


        //[ToDo] reshuffle and restart stimulus & ISI sequences here?


        //reset vars
        currentTime = 0;


        //check if block end condition is fullfilled
        if (currentConditionNo == 2 || currentConditionNo == 4)
        {
            //dual task walking conditions
            if (gaitPassCounter == gaitPassesPerBlock)
            {

                //set flag for experiment end and don't start another trial
                experimentEnd = true;
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

        }
        else
        {
            //start next trial
            StartTrial();

        }

    }//NextTrial()


    void StartTrial()
    {
        //initializes the start of a trial

        //reset trial time
        currentTime = 0.0f;

        //set ISI duration for current trial
        currentIsiDuration = isiDurations[trialCounter];
        //Debug.Log("currentIsiDuration: " + currentIsiDuration.ToString());


        //set stimuli for current trial
        if (currentConditionNo == 1 || currentConditionNo == 2)
        {
            //audio
            currentStimulus = audioStimuli[stimuliBlockSequence[trialCounter]];
        }
        else if (currentConditionNo == 3 || currentConditionNo == 4)
        {
            //visual
            currentStimulus = visualStimuli[stimuliBlockSequence[trialCounter]];
        }

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

    }


    void TriggerAudioStimulus(string side, string pitch)
    {
        //triggers an audio stimulus by sending a command to the Raspberry PI

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



    bool checkInsideGait()
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
        */
        return isInside;

    }//checkInsideGait()


    string getResponse()
    {
        string response = "";
        //checks if the participants did respond and returns the response

        //check if left controller button was pressed

        //check if right controller button was pressed


        return response;
    }


   


    static string BoolToString(bool b)
    {
        return b ? "true" : "false";
    }

}//class