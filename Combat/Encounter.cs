using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Character;
using RPG.Abilities;

namespace RPG.Battle
{
    public class Encounter : MonoBehaviour {
        //An "Enviroment_creature" (Usually an enemy) has this to start combat encounter

        //The range of grouping other allies into a encounter
        [SerializeField] float InitiateEncounterRadius = 5f;

        //A list of enemies in the encounter
        public List<CreatureContainer> GroupOfCreatures = new List<CreatureContainer>();

        public void InitiateEncounter(AbilityData abilityData)
        {
            
            List<Encounter> listOfCreatures = new List<Encounter>();

            //Get other creatures around this GameObjects position
            GetOtherEncounters(listOfCreatures, transform);
            //Get other creatures around the instigators position
            GetOtherEncounters(listOfCreatures, abilityData.GetUser().transform);
             
            if(1 < listOfCreatures.Count) { 

                EncounterManager.InitiateEncounter(listOfCreatures.ToArray(), abilityData);
            }
        }

        
        //Taking damage will effect all creatures in the group
        public void TakeDamage(int damage) {

            CreatureContainer[] temp = GroupOfCreatures.ToArray();
            foreach (var creature in temp) {
               if(creature.DamageTaken(damage)) GroupOfCreatures.Remove(creature);
            }

            if(GroupOfCreatures.Count == 0) GetComponent<Creature>().Die();
        }

        public int GetGroupStat(Stat stat) {

            int amount = 0;
            foreach (var creature in GroupOfCreatures) {

                switch (stat) {
                    case Stat.maxHealth:
                        amount = creature.GetCreatureController().GetStats(Stat.maxHealth);
                        break;
                    case Stat.damageTaken:
                        amount = creature.GetDamageTaken();
                        break;
                    default:
                        break;
                }
            }
            return amount;
            
        }
        private void GetOtherEncounters(List<Encounter> listOfCreatures, Transform overlapSphereStartPoint) {
            foreach (Collider collide in Physics.OverlapSphere(overlapSphereStartPoint.position, InitiateEncounterRadius)) {
                Encounter encounter = collide.GetComponent<Encounter>();
                if (encounter == null || listOfCreatures.Contains(encounter)) continue;

                listOfCreatures.Add(encounter);

            }
        }

    }
}

