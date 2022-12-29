using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using RPG.EnviromentManager;
using RPG.Abilities;
using Character;
using GameDevTV.Inventories;
using System.Linq;

namespace RPG.Battle
{
    public class EncounterManager : MonoBehaviour
    {
        //Persistent object as it will provide information
        public static EncounterManager Instance { get; private set; }
        public int combatMapIndex = 1;
        //Handles changes to the scene. Such as from open world to combat map
        WorldManager worldManager;

        

        //Update for each encounter
        //All the creatures that are starting combat in the open world
        public List<Encounter> worldCreatures { get; private set; } = new List<Encounter>();
        public CreatureContainer[] currentEncounter { get; private set; }
        AbilityData abilityData;
        //Prevent additional combats from starting
        bool isInCombat = false;

        private void Awake()
        {
            if (Instance) {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            worldManager = GetComponent<WorldManager>();
        }


        public static void SetCombatMapIndex(int combatMapIndex)
        {
            Instance.combatMapIndex = combatMapIndex;
        }

        //When an encounter occurs
        //The performed action and by whom
        public static void InitiateEncounter(Encounter[] currentEncounter, AbilityData abilityData)
        {
            if (Instance.isInCombat) return;

            abilityData.SetWorldState(WorldManager.State.Battle);
            Instance.worldCreatures = new(currentEncounter);

            List<CreatureContainer> creatureContainers = new List<CreatureContainer>();
            foreach (var encounter in currentEncounter) {
                foreach (var creatureContainer in encounter.GroupOfCreatures) {

                    //Equip the combat creatures with the open world creatures equipments
                    CreatureController creatureControl = creatureContainer.GetCreatureController();
                    if (creatureControl.playerControlled && creatureContainer.GetEquippedItems.Count < 1) {
                        Equipment equipment = encounter.GetComponent<Equipment>();
                        creatureContainer.SetEquippedItems(new(equipment.GetEquipedItems()));
                    }

                    creatureContainers.Add(creatureContainer);
                }
            }

            Instance.currentEncounter = creatureContainers.ToArray();
            Instance.abilityData = abilityData;

            Instance.StartBattleScene();

        }

        private void StartBattleScene()
        {
            
            worldManager.WorldTransitionToBattle(combatMapIndex);
            isInCombat = true;

        }

        public static async void StartWorldScene()
        {
            Instance.isInCombat = false;
            //Create an action that kills the defeted enemies and changes the players health to what they
            //had when combat did end
            System.Action KillAllBeforeFade = () => { 
                
                Instance.KillAllDefeatedEnemies();
                Encounter playerEncounter = Instance.worldCreatures.Where(x => x.tag == "Player").Single();
                int updateHealth = BattleManager.battleManager.GetTurnOrder().Where(x => 
                    x.playerControlled).Single().GetStats(Stat.damageTaken);
                playerEncounter.GetComponent<Character.Stats>().SetDamageTaken(updateHealth);
            };

            await Instance.worldManager.BattleTransitionToWorld(KillAllBeforeFade);
            
        }


        public static EncounterInfo GetEncounterInfo() => new EncounterInfo(
            Instance.currentEncounter, Instance.abilityData);


        public async void SetupCombat(List<CreatureController> battleEvent, EncounterInfo startEncounterInfo) {

            BattleManager battleManager = BattleManager.battleManager;
            foreach (CreatureController creature in battleEvent) {
                battleManager.AddToTurnOrder(creature.GetComponent<Creature>());
                creature.SubToCreatureDeath(battleManager);
                creature.SubToEvent(battleManager);

            }

            Task fader = BattleUI.battleUI.FadeBlackScreen(WorldUI.Fade.In, 1f);

            await fader;

            battleManager.PerformInstagetorAction(startEncounterInfo);
        }


        private void KillAllDefeatedEnemies() {

            foreach (var creature in worldCreatures) {
                CreatureController creatureController = creature.GetComponent<CreatureController>();

                if(creatureController.tag != "Player")
                    creatureController.creature.Die();
            }
        }
    }

    public struct EncounterInfo{

        CreatureContainer[] currentEncounter;
        AbilityData abilityData;

        public EncounterInfo(CreatureContainer[] currentEncounter, AbilityData abilityData)
        {
            this.currentEncounter = currentEncounter;
            this.abilityData = abilityData; 
        }

        public CreatureContainer[] GetCreatures() => currentEncounter;
        public AbilityData GetAbilityData() => abilityData;
    }
}

