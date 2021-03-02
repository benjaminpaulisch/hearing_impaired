using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerClicked : MonoBehaviour
{
    public ExperimentManager expManager;

    private SteamVR_TrackedController _controller;
    private string side = "";

    // Start is called before the first frame update
    void Start()
    {
        //set side
        if (gameObject.name.Contains("left"))
        {
            side = "left";
        }
        else if (gameObject.name.Contains("right"))
        {
            side = "right";
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnEnable()
    {
        _controller = GetComponent<SteamVR_TrackedController>();
        _controller.TriggerClicked += HandleTriggerClicked;
    }

    private void OnDisable()
    {
        _controller.TriggerClicked -= HandleTriggerClicked;
    }


    private void HandleTriggerClicked(object sender, ClickedEventArgs e)
    {
        //check if response is active
        if (expManager.GetResponseActive())
        {
            //write lsl marker
            expManager.marker.Write("controller response: " + side);
            Debug.Log("Response given: " + side);

            expManager.SetTriggerPressed(side);
        }
        else
        {
            //write lsl marker
            expManager.marker.Write("controller response (outside response time window): " + side);
            Debug.Log("Controller trigger pressed: " + side);
        }

    }

}
