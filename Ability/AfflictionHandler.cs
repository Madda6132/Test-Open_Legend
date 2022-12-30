using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RPG.Abilities;
using RPG.Stats;
using RPG.Abilities.Affliction;
using RPG.Observers;
using System.Linq;
using System;

namespace Character
{

    public class AfflictionHandler : MonoBehaviour, IModifierProvider, IEvent, IListener, IActionEvent
    {
        //Dictionary of afflictions and the stored data related to it
        Dictionary<AfflictionAbility, AfflictionDataHandelar> durationHandalers = 
            new Dictionary<AfflictionAbility, AfflictionDataHandelar>();
        public delegate void UpdateAffliction(Dictionary<AfflictionAbility, AfflictionDataHandelar> durationHandalers);
        public event UpdateAffliction UpdateAfflictionEvent;

        [Header("Testing rules")]
        [SerializeField] BaseActionRule[] rules = new BaseActionRule[0];

        [Tooltip("Apply the affliction(s) on startup")]
        public AfflictionAbility[] startupAffliction;
        [SerializeField] AbilityScriptObject ability;

        CreatureController creatureController;
        IEvent startTurnEvent = null;

        private void Awake()
        {
            creatureController = GetComponent<CreatureController>();
            startTurnEvent = creatureController;
            SubToEvent(startTurnEvent);
            
        }

        private void Start() {
            StartupAffliction();
        }

        private void Update()
        {
            if(durationHandalers != null)
            {
                Dictionary<AfflictionAbility, AfflictionDataHandelar> tempHolder = new Dictionary<AfflictionAbility, AfflictionDataHandelar>(durationHandalers);
                foreach (var key in tempHolder.Keys)
                {
                    tempHolder[key].Update();
                }
            }
                

        }

        /// <summary>
        /// Needs a ability to be set and startupAffliction
        /// !TODO: REMOVE THE ABILITY LIMITATION!
        /// </summary>
        private void StartupAffliction() {

            
            foreach (var affliction in startupAffliction) {
                TakeAffliction(new AbilityData(gameObject, ability, false, RPG.EnviromentManager.WorldManager.GetWorldState()), affliction);
            }
            
        }

        /// <summary>
        /// Change turn by - / + 
        /// </summary>
        public void StartTurn() {

            int changeTurn = -1;

            if (durationHandalers != null)
            {
                Dictionary<AfflictionAbility, AfflictionDataHandelar> tempHolder = new Dictionary<AfflictionAbility, AfflictionDataHandelar>(durationHandalers);
                foreach (var key in tempHolder.Keys)
                {
                    tempHolder[key].ChangeTurnDuration(changeTurn);
                }

                ResistBanes();
            }
            

        }

        public void TakeAffliction(AbilityData abilityData, AfflictionAbility afflictionAbility, int stacks = 1)
        {

            //If(character immune/resistant) Resist or add modifier to the defense
            
            //Is the character currently effected by the same effect call duplicate method
            if (durationHandalers.ContainsKey(afflictionAbility))
            {
                foreach (var key in durationHandalers.Keys)
                {
                    if (afflictionAbility == key) {
                        key.DuplicateAffliction(durationHandalers[key]);
                        break;
                    }
                    
                } 
            }
            else
            {
                durationHandalers.Add(afflictionAbility, new AfflictionDataHandelar(afflictionAbility, abilityData, gameObject, stacks));
                
            }

            if (0 < durationHandalers[afflictionAbility].modifierProviders.Count) {

                UpdateModifier(durationHandalers[afflictionAbility]);
            }

            UpdateAfflictionEvent?.Invoke(durationHandalers);
                
            
            creatureController.DisplayPopupResult(
                afflictionAbility.GetIcon(), "+", afflictionAbility.GetAfflictionTypeColor());
            
        }

        public void RemoveAffliction(AfflictionDataHandelar afflictionDataHandelar, bool popupDisplay = true)
        {

            if (durationHandalers.ContainsValue(afflictionDataHandelar))
            {
                AfflictionAbility tempKey = null;
                foreach (var key in durationHandalers.Keys)
                {
                    if (afflictionDataHandelar == GetAfflictionDataHandelar(key))
                    {
                        tempKey = key;
                        break;
                    }

                }

                durationHandalers.Remove(tempKey);

                foreach (var block in  new List<AfflictionBuildingBlock>(tempKey.GetStartEventBlock)) {
                    afflictionDataHandelar.RemoveStrategyEvent(block);
                }

                if(popupDisplay) creatureController.DisplayPopupResult(tempKey.GetIcon(), "-", tempKey.GetAfflictionTypeColor());
            }

            if(0 < afflictionDataHandelar.modifierProviders.Count) { 

                UpdateModifier(afflictionDataHandelar);
            }

            UpdateAfflictionEvent?.Invoke(durationHandalers);
            
        }


        public AfflictionDataHandelar GetAfflictionDataHandelar(AfflictionAbility AfflictionAbility) => 
            durationHandalers[AfflictionAbility];

        public Dictionary<AfflictionAbility, AfflictionDataHandelar> GetActiveAfflictions() => 
            durationHandalers;

        //To resist debuffs(Banes) action
        public void ResistBanes() {

            List<KeyValuePair<AfflictionAbility, AfflictionDataHandelar>> baneList = 
                new(durationHandalers.Where(x => 
                x.Key.GetAfflictionType() == AfflictionAbility.AfflictionType.Bane).ToList());

            foreach (var bane in baneList) {

                //Check if it can be resisted since after 3 attempts the creature cant
                if(bane.Value.CanBeResisted()) {
                   int resistRoll = UnityEngine.Random.Range(1, 21);

                    if(10 <= resistRoll) {

                        RemoveAffliction(bane.Value, false);
                        creatureController.DisplayPopupResult(bane.Key.GetIcon(), 
                            $"- Successfully resisted ({resistRoll})", bane.Key.GetAfflictionTypeColor());
                    } else {

                        creatureController.DisplayPopupResult(bane.Key.GetIcon(), 
                            $" Failed to resist ({resistRoll})", bane.Key.GetAfflictionTypeColor());
                    }
                }
            }

        }

        public void SubToEvent(IListener observer)
        {
            UpdateAfflictionEvent += observer.EventTriggerMethod;
            UpdateAfflictionEvent.Invoke(durationHandalers);
        }

        public void UnSubToEvent(IListener observer)
        {
            UpdateAfflictionEvent -= observer.EventTriggerMethod;
        }

        //!TODO: CHANGE EVENT TO CALL THE METHOD DIRECTLY!
        public void EventTriggerMethod(object stuff) {
            StartTurn();
        }

        public void SubToEvent(IEvent theEvent) {
            theEvent.SubToEvent(this);
        }

        public void UnSubToEvent(IEvent theEvent) {
            theEvent.UnSubToEvent(this);
        }

        //Get modifier by Attribute stat
        public IEnumerable<int> GetAdditionalModifier(Stat stat) {

            foreach (var duration in durationHandalers) {
                foreach (var modifier in duration.Value.GetAdditionalModifier(stat)) {
                    yield return modifier;
                } 
            }
        }

        //Get modifier by ActionModifiers information (Example, Attack, AttributeStat, name of action)
        public IEnumerable<int> GetAdditionalModifier(ActionModifier actionModifier) {

            foreach (var duration in durationHandalers) {
                foreach (var modifier in duration.Value.GetAdditionalModifier(actionModifier)) {
                    yield return modifier;
                }
            }
        }

        //Get rules by phase in dice rolling
        public IEnumerable<(BaseActionRule actionEvent, DiceRollingPhase actionPhase)> GetAdditionalEvent(
            AbilityData abilityData, ActionModifier actionModifier) {

            foreach (var rule in rules) {
                foreach (var item in rule.GetAdditionalEvent(abilityData, actionModifier)) {
                    yield return item;
                }

            }
        }
        private void OnDestroy() {
            UnSubToEvent(startTurnEvent);
        }

        private void UpdateModifier(AfflictionDataHandelar durationHandalers) {

            Stats stats = GetComponent<Stats>();
            foreach (var modifier in durationHandalers.modifierProviders) {
                foreach (var stat in modifier.GetStatTypes()) {
                    stats.UpdateStatValue(stat);
                } 
                
            }
        }

        private void UpdateModifier() {

        Stats stats = GetComponent<Stats>(); 
            foreach (var stat in GetStatTypes()) {
                stats.UpdateStatValue(stat);
            }
        }

        public IEnumerable<Stat> GetStatTypes() {

            List<Stat> statTypes = new List<Stat>();
            foreach (var data in durationHandalers.Where(x => 0 < x.Value.modifierProviders.Count).
                Select(x => x.Value)) {
                 
                foreach (var modify in data.GetStatTypes()) {
                    if (!statTypes.Contains(modify)) {
                        statTypes.Add(modify);
                        yield return modify;
                    }

                }
            }
        }
    }
}



