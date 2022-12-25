using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Character;

public class EnemyLocomotionManager : MonoBehaviour
{
    
    NavMeshAgent navMeshAgent;
    AnimatorManager animatorManager;

     

    [Header("A.I behavior")]
    public float stoppingDistnace; 

    bool isInteractiong = false;

    float navMeshAgentStartSpeed = 6;

    private void Awake()
    {
        
        navMeshAgent = GetComponent<NavMeshAgent>();
        animatorManager = GetComponent<AnimatorManager>();
        navMeshAgentStartSpeed = navMeshAgent.speed;
    }

    private void Update()
    {
        
        UpdateAnimation();
        CheckInteraction();
    }

    private void CheckInteraction()
    {
        isInteractiong = animatorManager.GetAnimatorBool("IsInteracting");
    }

   

    public void MoveToTarget(Vector3 pos, TravleSpeed travleSpeed = TravleSpeed.Run)
    {
        
        if (isInteractiong) return;
        switch (travleSpeed)
        {
            case TravleSpeed.Walk:
                navMeshAgent.speed = navMeshAgentStartSpeed * 0.2f;
                break;

            default:
            case TravleSpeed.Run:
                navMeshAgent.speed = navMeshAgentStartSpeed;
                break;

        }

        ActivateMovement(true);
        navMeshAgent.SetDestination(pos);
        
        
    }

    public void ActivateMovement(bool AllowMovement)
    {
        navMeshAgent.isStopped = !AllowMovement;
    }
    

    private void UpdateAnimation()
    {
        Vector3 velocity = navMeshAgent.velocity;

        if (navMeshAgentStartSpeed != 0) velocity /= navMeshAgentStartSpeed;

        Vector3 localVelocity = transform.InverseTransformDirection(velocity);
        
        animatorManager.UpdateAnimatorValues(0, localVelocity.z, false);
    }


    public enum TravleSpeed
    {
        Walk,
        Run
    }

}
