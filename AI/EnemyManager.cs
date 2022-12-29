using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RPG.Item;
using Character;
using GameDevTV.Saving;
using RPG.Observers;
using InventoryExample.Control;
using RPG.Abilities;
using RPG.EnviromentManager;

public class EnemyManager : MonoBehaviour, ISaveable, IObserverDeath
{
    public EquipmentSlotManager equipmentSlotManager;

    [SerializeField] AbilityScriptObject attackAbility;
    [SerializeField] Transform eyePosition;
    [SerializeField] PatrolPath patrolPath;

    EnemyLocomotionManager enemyLocomotionManager;
    PlayerAttack playerAttack;

    [Header("A.I Settings")]
    public float detectionRadius = 20;
    //DetectionAngle is the angle in front of the creature
    //Min is left of the creature
    //Max is the right of the creature
    public float minimumDetectionAngle = -50f;
    public float maximumDetectionAngle = 50f;
    public float interestTimer = 3f;
    public LayerMask detectionLayer;

    [SerializeField] float rotationSpeed = 2;

    public bool isPreformingAction;

    
    float timer = Mathf.Infinity;

    public GameObject currentTarget { private set; get; }
    State currentState = State.Pattrol;
    bool deactivateManager = false;

    enum State {
        Pattrol,
        Inspect,
        Attack
    }

    Vector3 guardPosition;
    int waypointIndex = 0;


    private void OnDrawGizmosSelected()
    {
        //Display Enemy field of vision
        Quaternion upRayRotation = Quaternion.AngleAxis(minimumDetectionAngle, Vector3.up);
        Quaternion downRayRotation = Quaternion.AngleAxis(maximumDetectionAngle, Vector3.up);

        Vector3 upRayDirection = upRayRotation * transform.forward * detectionRadius;
        Vector3 downRayDirection = downRayRotation * transform.forward * detectionRadius;

        Gizmos.DrawRay(transform.position + Vector3.up, upRayDirection);
        Gizmos.DrawRay(transform.position + Vector3.up, downRayDirection);
        Gizmos.DrawLine(transform.position + Vector3.up + downRayDirection, 
            transform.position + Vector3.up + upRayDirection);
    }


    private void Awake()
    {
        enemyLocomotionManager = GetComponent<EnemyLocomotionManager>();
        equipmentSlotManager = GetComponent<EquipmentSlotManager>();
        playerAttack = GetComponent<PlayerAttack>();
        GetComponent<CreatureController>().SubToCreatureDeath(this);

        if (patrolPath != null) { 

            guardPosition = patrolPath.GetWaypoint(waypointIndex); 
        } else {

            guardPosition = transform.position;
        }
    }
     
    // Update is called once per frame
    void Update()
    {
        if (deactivateManager) return;

        DetectTargets();
        HandleCurrentState();

        timer += Time.deltaTime;
    }

    public void DetectTargets() {
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, detectionLayer);

        for (int i = 0; i < colliders.Length; i++) {
            if (colliders[i].tag == "Player") {
                Transform targetTransform = colliders[i].GetComponent<Transform>();

                //Check if creature is facing target
                Vector3 targetDirection = targetTransform.position - transform.position;
                float viewableAngle = Vector3.Angle(targetDirection, transform.forward);

                if ((viewableAngle > minimumDetectionAngle && viewableAngle < maximumDetectionAngle) || 
                    currentState == State.Attack) {

                    float dis = Vector3.Distance(transform.position + Vector3.up, targetTransform.position + Vector3.up);
                    //Check if a object is in the way
                    RaycastHit hit;
                    Ray ray = GetRayTowardsTarget(colliders[i].transform, colliders[i].bounds.max.y);

                    Debug.DrawRay(ray.origin, ray.direction, Color.green, 2f);
                    if (Physics.Raycast(ray, out hit, dis) && hit.collider.tag != "Player")
                        return;

                    if (!targetTransform.GetComponent<CreatureController>().creature.isDead)
                        currentTarget = targetTransform.gameObject;

                    playerAttack.SetTarget(currentTarget);
                    ChangeState(State.Attack);
                }
            }
        }
    }

    public object CaptureState() {

        return new SaveState(guardPosition);
    }

    public void RestoreState(object state) {
        guardPosition = ((SaveState)state).GetVector();
    }

    public void uponDeath(Creature sender) {
        deactivateManager = true;
        enemyLocomotionManager.ActivateMovement(false);
    }

    private void HandleCurrentState()
    {
         
        switch (currentState)
        {
            case State.Pattrol:
                PattrolBehaviour();
                break;

            case State.Inspect:
                if (timer >= interestTimer) ChangeState(State.Pattrol);
                currentTarget = null;
                playerAttack.SetTarget(currentTarget);
                //Wait before going back to patroll
                break;

            case State.Attack:
                AttackBehaviour();

                break;
            default:
                break;
        }

        
    }

    private void PattrolBehaviour()
    {

        if(patrolPath != null)
        {
            if (DistanceToTarget(guardPosition) <= 1f)
            {
                waypointIndex = patrolPath.GetNextIndex(waypointIndex);
                guardPosition = patrolPath.GetWaypoint(waypointIndex);

            }
        }

        enemyLocomotionManager.MoveToTarget(guardPosition, EnemyLocomotionManager.TravleSpeed.Walk);
    }

    private void AttackBehaviour()
    {
        //Check if target is dead or interest time ran out
        if (currentTarget.GetComponent<CreatureController>().creature.isDead || timer >= interestTimer)
        {
            ChangeState(State.Inspect);
            return;
        }

        

        //If the target is within reach of the weapon equipped 
        if (DistanceToTarget(currentTarget.transform.position) <= equipmentSlotManager.currentlyEquipedWeapon.Range)
        {
            RaycastHit hit;
            Ray ray = GetRayTowardsTarget(currentTarget.transform, currentTarget.GetComponent<Collider>().bounds.max.y);

            if (Physics.Raycast(ray, out hit, Mathf.RoundToInt(equipmentSlotManager.currentlyEquipedWeapon.Range + 
                0.5f)) && hit.collider.tag != "Player")
            {
                enemyLocomotionManager.MoveToTarget(currentTarget.transform.position);
                return;
            }

            enemyLocomotionManager.ActivateMovement(false);
            AttackTarget();

        }
        else
        {

            enemyLocomotionManager.MoveToTarget(currentTarget.transform.position);
        }

    }

    private void ChangeState(State state)
    {
        currentState = state;
        timer = 0;
    }

    private float DistanceToTarget(Vector3 pos) => Vector3.Distance(pos, transform.position);



    private void AttackTarget()
    {
        if (WorldManager.GetWorldState() == WorldManager.State.Battle) return;

        transform.rotation =  Quaternion.RotateTowards(transform.rotation, 
            Quaternion.LookRotation((currentTarget.transform.position - transform.position).normalized), 
            rotationSpeed * Time.deltaTime);
         
        
        attackAbility.Use(gameObject, true); 
        
    }

    private Ray GetRayTowardsTarget(Transform target, float targetHeight) =>
        new Ray(eyePosition.position, ((target.position +
                (Vector3.up * targetHeight * 0.75f)) - eyePosition.position));

    [System.Serializable]
    private class SaveState {

        float x = 0;
        float y = 0;
        float z = 0;
        public SaveState(Vector3 pos)
        {
            this.x = pos.x;
            this.y = pos.y;
            this.z = pos.z;
        }

        public Vector3 GetVector()
        {
            return new Vector3(x, y, z);
        }
    }

    

}
