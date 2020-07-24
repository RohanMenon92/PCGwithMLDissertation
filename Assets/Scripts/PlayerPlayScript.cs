using Invector.vCharacterController;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PlayerPlayScript : MonoBehaviour
{
    public bool thirdPersonPlayer;

    public float noPathFOV = 120f;
    public float noPathFOVGain = 0.2f;

    public Transform aimTarget;
    public Transform aimAgent;

    public GameObject inCompletePathPrefab;
    public GameObject completePathPrefab;

    Camera camera;
    NavMeshAgent navMeshAgent;

    List<GameObject> pathObjects = new List<GameObject>();

    // Start is called before the first frame update
    void Start()
    {
        camera = FindObjectOfType<Camera>();
        navMeshAgent = this.GetComponent<NavMeshAgent>();

        // Set Which kind of control is enabled
        navMeshAgent.enabled = !thirdPersonPlayer;
        this.GetComponent<vThirdPersonInput>().enabled = thirdPersonPlayer;
        this.GetComponent<Rigidbody>().useGravity = thirdPersonPlayer;

        aimTarget.gameObject.SetActive(!thirdPersonPlayer);
        aimAgent.gameObject.SetActive(!thirdPersonPlayer);
        camera.GetComponent<vThirdPersonCamera>().defaultDistance = thirdPersonPlayer ? 5 : 20;
        camera.GetComponent<vThirdPersonCamera>().height = thirdPersonPlayer ? 2 : 40;

        if(!thirdPersonPlayer)
        {
            camera.GetComponent<vThirdPersonCamera>().enabled = false;
            camera.transform.SetParent(transform);
            camera.transform.position = new Vector3(0f, 80f, -10f);
            aimAgent.SetParent(transform);
            aimAgent.localPosition = new Vector3(0f, 5f, 0f);
            navMeshAgent.Warp(new Vector3(0f, 10f, 0f));
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

        //  haspath is not very reliable, better to check if the remaining distance is below a certain threshold
        if(!navMeshAgent.hasPath || navMeshAgent.remainingDistance < 0.2f)
        {
            //if(pathObjects.Count > 0)
            //{
            //    ClearPathObjects();
            //}

            camera.transform.LookAt(transform);
            if(camera.fieldOfView < noPathFOV)
            {
                camera.fieldOfView += noPathFOVGain;
            }
        } else
        {
            camera.transform.LookAt((transform.position + navMeshAgent.destination)/2);

            float navMeshAgentDistance = Vector3.Distance(transform.position, navMeshAgent.destination);
            camera.fieldOfView = navMeshAgentDistance < 90 ? navMeshAgentDistance + 10 : 90;
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
                    MoveAgentTo(hit.point);
                }
            }
        }
    }

    private void ClearPathObjects()
    {
        // Pooling?
        // Reverse iterate for removal
        for (int i = pathObjects.Count - 1; i >= 0; i--)
        {
            GameObject removedObject = pathObjects[i];
            GameObject.Destroy(removedObject);
        }
        pathObjects.Clear();
    }

    private void MoveAgentTo(Vector3 point)
    {
        ClearPathObjects();

        if (!aimAgent.gameObject.activeSelf)
        {
            aimAgent.gameObject.SetActive(true);
        }

        NavMeshPath path = new NavMeshPath();
        navMeshAgent.CalculatePath(point, path);

        switch(path.status)
        {
            case NavMeshPathStatus.PathComplete:
                Debug.Log("PATH IS POSSIBLE!!!! :) ");

                navMeshAgent.SetDestination(point);
                if (!aimTarget.gameObject.activeSelf)
                {
                    aimTarget.gameObject.SetActive(true);
                }
                aimTarget.position = point;

                foreach (Vector3 pathPoint in path.corners)
                {
                    // Pooling?
                    GameObject pathObject = GameObject.Instantiate(completePathPrefab);
                    pathObject.transform.position = pathPoint;
                    pathObjects.Add(pathObject);
                }
                break;
            case NavMeshPathStatus.PathPartial:
                Debug.Log("ONLY PARTIAL PATH IS POSSIBLE... :( ");
                foreach (Vector3 pathPoint in path.corners)
                {
                    // Pooling?
                    GameObject pathObject = GameObject.Instantiate(inCompletePathPrefab);
                    pathObject.transform.position = pathPoint;
                    pathObjects.Add(pathObject);
                }
                break;
            case NavMeshPathStatus.PathInvalid:
                Debug.Log("NO PATH POSSIBLE... :( ");
                break;
        }
    }
}
