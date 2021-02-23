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
        expmanager.IncrementInsideGaitCounter();

        //lsl marker
        expmanager.marker.Write(other.name + " entered OptoGait object");
        print(other.name + " entered OptoGait object");

    }

    private void OnTriggerExit(Collider other)
    {
        expmanager.DecrementInsideGaitCounter();

        //lsl marker
        expmanager.marker.Write(other.name + " exited OptoGait object");
        print(other.name + " exited OptoGait object");

    }

}
