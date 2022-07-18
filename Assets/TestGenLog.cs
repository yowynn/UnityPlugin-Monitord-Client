using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestGenLog : MonoBehaviour
{
    // Start is called before the first frame update
    private void Start()
    {
        Debug.Log("haha");
        InvokeRepeating("ShowTimeLog", 0, 1);
    }

    // Update is called once per frame
    private void Update()
    {
    }

    private void ShowTimeLog()
    {
        Debug.Log($"time is : {Time.realtimeSinceStartup}");
    }
}