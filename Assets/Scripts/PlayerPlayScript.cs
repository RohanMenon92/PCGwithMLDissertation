using Invector.vCharacterController;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PlayerPlayScript : MonoBehaviour
{
    public bool thirdPersonPlayer;

    public float noPathFOV = 90f;
    public float noPathFOVGain = 0.1f;

    public Transform aimTarget;

    Camera camera;
    NavMeshAgent navMeshAgent;

    // Start is called before the first frame update
    void Start()
    {
        camera = FindObjectOfType<Camera>();
        navMeshAgent = this.GetComponent<NavMeshAgent>();

        // Set Which kind of control is enabled
        navMeshAgent.enabled = !thirdPersonPlayer;
        this.GetComponent<vThirdPersonInput>().enabled = thirdPersonPlayer;

        aimTarget.gameObject.SetActive(!thirdPersonPlayer);
        camera.GetComponent<vThirdPersonCamera>().defaultDistance = thirdPersonPlayer ? 5 : 20;
        camera.GetComponent<vThirdPersonCamera>().height = thirdPersonPlayer ? 2 : 40;

        if(!thirdPersonPlayer)
        {
            camera.GetComponent<vThirdPersonCamera>().enabled = false;
            camera.transform.SetParent(transform);
            camera.transform.position += new Vector3(0f, 40f, 0f);
            navMeshAgent.Warp(Vector3.one);
        }
    }

    // Update is called once per frame
    void Update()
    {
        CheckNavMeshInput();
    }

    private void CheckNavMeshInput()
    {
        if(thirdPersonPlayer || !navMeshAgent.enabled)
        {
            return;
        }

        if(!navMeshAgent.hasPath)
        {
            if(aimTarget.gameObject.activeSelf)
            {
                aimTarget.gameObject.SetActive(false);
            }
            camera.transform.LookAt(transform);
            if(camera.fieldOfView < noPathFOV)
            {
                camera.fieldOfView += noPathFOVGain;
            }
        } else
        {
            camera.transform.LookAt((transform.position + navMeshAgent.destination)/2);

            camera.fieldOfView = Vector3.Distance(transform.position, navMeshAgent.destination) + 2;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                camera.transform.LookAt(hit.point);

                if (hit.transform.name.Contains("TerrainChunk"))
                {
                    if (!aimTarget.gameObject.activeSelf)
                    {
                        aimTarget.gameObject.SetActive(true);
                    }
                    navMeshAgent.SetDestination(hit.point);
                    aimTarget.position = navMeshAgent.destination;
                }
            }
        }
    }
}
