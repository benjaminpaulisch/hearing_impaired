using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerInsideDetection : MonoBehaviour
{
    public ExperimentManager expmanager;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    
    private void OnTriggerEnter(Collider other)
    {
        //lsl marker
        expmanager.marker.Write(other.name + " entered OptoGait");
        print(other.name + " entered OptoGait");

        //only increment if feet collided
        //if (other.name.Contains("Foot"))
        if (other.name.Contains("Torso"))
        {
            expmanager.IncrementInsideGaitCounter();
        }

    }

    private void OnTriggerExit(Collider other)
    {
        //lsl marker
        expmanager.marker.Write(other.name + " exited OptoGait");
        print(other.name + " exited OptoGait");

        //only dencrement if feet collided
        //if (other.name.Contains("Foot"))
        if (other.name.Contains("Torso"))
        {
            expmanager.DecrementInsideGaitCounter();
        }

    }

}
