using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Character;
using RPG.Event;
using RPG.Observers;
using RPG.Battle;
using RPG.Abilities;
using System.Threading.Tasks;

public class BattleManager : MonoBehaviour, IObserverDeath, IEvent {

    
    List<CreatureController> turnOrder = new List<CreatureController>();
    int currentTurn = 0;

    [SerializeField] float waitTime = 5f;

    public delegate void Turn(object stuff);
    public event Turn startTurn;

    public static BattleManager battleManager; 
    bool stopTurn = false;


    private void Awake()
    {
        if(battleManager != null)
        {
            Destroy(battleManager.gameObject);
        }
        battleManager = this;
    }

    public CreatureController GetCurrentTurn() => turnOrder[currentTurn];
    public List<CreatureController> GetTurnOrder() => turnOrder;

    //Performs the creature ability that started the encounter
    public void PerformInstagetorAction(EncounterInfo encounterInfo)
    {
        if (encounterInfo.GetAbilityData() == null) return;


        GameObject user = null;
        List<GameObject> victims = new List<GameObject>();

        foreach (CreatureController creature in turnOrder)
        {
            if (creature.creature.GetCreatureID() == 
                encounterInfo.GetAbilityData().GetUser().GetComponent<Creature>().GetCreatureID()) user = creature.gameObject;

        }

        foreach (GameObject target in encounterInfo.GetAbilityData().GetTargets())
        {
            foreach (CreatureController creature in turnOrder)
            {
                if ((creature.GetComponent<Creature>().GetCreatureID() ==
                target.GetComponent<Creature>().GetCreatureID()) && !victims.Contains(creature.gameObject)) {
                    victims.Add(creature.gameObject);
                    break;
                }
                
            }
        }

        if (user == null || victims.Count == 0) return;

        AbilityData abilityData = encounterInfo.GetAbilityData();
        abilityData.AddFinishAction( StartCreaturesTurn);
        abilityData.SetTargets(victims);
        abilityData.SetUser(user);

        abilityData.GetAbility().ForceUseAbility(abilityData);
    }

    public void uponDeath(Creature creature)
    {
        Debug.Log(creature.name + " died");
        CreatureController removeCreature = creature.GetComponent<CreatureController>();
        CreatureController currentCreaturesTurn = GetCurrentTurn();

        removeCreature.UnSubToEvent(this);
        if (turnOrder.Contains(removeCreature)) turnOrder.Remove(removeCreature);
        if (turnOrder.Contains(currentCreaturesTurn)) currentTurn = turnOrder.IndexOf(currentCreaturesTurn);
         
        if (creature.tag == "Player") BattleLost();
        if (turnOrder.Count == 1 && turnOrder[0].tag == "Player") BattleWon();
    }
    
    public void NextTurn()
    {
        if (stopTurn) return;

        GetCurrentTurn()?.EndTurn();

        currentTurn++;

        if (currentTurn >= turnOrder.Count) currentTurn = 0;
        StartCreaturesTurn();
    }

    public void AddToTurnOrder(Creature creature) {

        if (turnOrder.Count == 0)
        {
            turnOrder.Add(creature.GetComponent<CreatureController>());
            return;
        }
        int creatureInitiative = ((int)creature.GetStat(Stat.Initiative));
        int indexInTurnOrder = turnOrder.Count;
        foreach (CreatureController turnCreature in turnOrder)
        {
            int turnCreatureInitiative = ((int)turnCreature.GetComponent<Creature>().GetStat(Stat.Initiative));
            
            if (turnCreatureInitiative < creatureInitiative &&
                indexInTurnOrder > turnOrder.IndexOf(turnCreature))
            {

                indexInTurnOrder = turnOrder.IndexOf(turnCreature);
                if (indexInTurnOrder == 0) break;
            }
        }

        if (indexInTurnOrder == turnOrder.Count)
        {
            turnOrder.Add(creature.GetComponent<CreatureController>());
        }
        else
        {
            turnOrder.Insert(indexInTurnOrder, creature.GetComponent<CreatureController>());
        }
    }

    public void SubToEvent(IListener observer)
    {
        startTurn += observer.EventTriggerMethod;
    }

    public void UnSubToEvent(IListener observer)
    {
        startTurn -= observer.EventTriggerMethod;
    }

    private void BattleWon() {
        stopTurn = true;
        BattleUI.battleUI.WinResult();
        //Give Reward? In battle Scene or World Scene? 

        Debug.Log("You Win");
    }

    private void BattleLost() {
        stopTurn = true;
        BattleUI.battleUI.LoseResult();
        //Open menu (If no save start main menu automatically)

        Debug.Log("You Lose");
    }

    private void StartCreaturesTurn() {
        if (turnOrder.Count > 0) {

            startTurn.Invoke(turnOrder[currentTurn]);
        }

    }
}
