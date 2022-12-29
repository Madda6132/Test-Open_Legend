using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RPG.Abilities.Targeting;
using RPG.EnviromentManager; 
using Character;
using RPG.Battle.UI;
using System.Threading.Tasks;
using System.Linq;


namespace RPG.Abilities {
    [CreateAssetMenu(fileName = "Ability", menuName = "Ability/NewAbility", order = 0)]

    public class AbilityScriptObject : ScriptableObject, ISerializationCallbackReceiver
    {

        // CONFIG DATA
        [Tooltip("Auto-generated UUID for saving/loading. Clear this field if you want to generate a new one.")]
        [SerializeField] string itemID = null;
        [Tooltip("Item name to be displayed in UI.")]
        [SerializeField] string displayName = null;
        [Tooltip("Item description to be displayed in UI.")]
        [SerializeField] [TextArea] string description = null;
        [Tooltip("The UI icon to represent this item in the inventory.")]
        [SerializeField] Sprite icon = null;
        [Tooltip("Determine what type it is. (Example Attack, Defend, Ability)")]
        [SerializeField] ActionHandeler.AbilityType abilityType; 
        [SerializeField] int actionPointCost = 1;

        //Affliction can be both buffs and debuffs that causing an effect during certain times and/or turns
        [Tooltip("Insert for bane/boon Ability")]
        [SerializeField] AfflictionAbility[] afflictionAbility;

        [Header("Target Strategy")]
        [Tooltip("How the user performs the ability out of combat (Example, Stand still, swing weapon)")]
        [SerializeField] WorldTargetStrategy worldTargetStrategy;
        [Tooltip("How the user performs the ability in combat (Example, Move up to target, swing weapon)")]
        [SerializeField] BattleTargetStrategy battleTargetStrategy;

        [Header("Properties")]
        [Tooltip("The effect on the ability's targets (Example, taking damage)")]
        [SerializeField] EffectStrategy[] effectStrategies;
        [Tooltip("Filter out targets (Example, filter out friendly units)")]
        [SerializeField] FilterStrategy[] filterStrategies;

        //(In turn based combat) When a creature uses an ability they stay put or move
        public BattlePosition battlePosition;

        [Tooltip("Animation when ability is activated")]
        public EffectStrategy activatedAbilityAnimation;

        public float cooldownTime;
        public int cooldownTurn;

        [Header("Attribute targets Defence")]
        [SerializeField] List<AttributeVSStat> attributeVSStats = new();

        [Header("Action information")]
        [SerializeField] Stat useStat = Stat.Might;
        [SerializeField] ActionType actionType;
        [SerializeField] ActionKeyWords actionKeyWords;
        [SerializeField] string[] actionTags;
        
        public enum BattlePosition
        {
            Stayput,
            MiddleOfBattle,
            ToTarget
        }

        public Sprite GetIcon() => icon;
        public string GetItemID() => itemID;
        public string GetDisplayName() => displayName;
        public string GetDescription() => description;
        public ActionHandeler.AbilityType GetAbilityType() => abilityType;
        public int GetActionScoreCost => actionPointCost;
        public AfflictionAbility[] GetAffliction => afflictionAbility;
        public Stat GetAbilityAttributeUseadge => useStat;
        public FilterStrategy[] GetFilter() => filterStrategies;
        public ActionModifier GetActionModifier => 
            new ActionModifier(0, actionType, actionKeyWords, GetActionTag());
        public Stat GetAbilityTargetDefence(Stat stat) => attributeVSStats.Where(x =>
            x.ChecKCompatebility(stat)).FirstOrDefault().GetVSStat;

        /// <summary>
        /// Trigger the use of this item. Override to provide functionality.
        /// </summary>
        /// <param name="user">The character that is using this action.</param>
        public virtual void Use(GameObject user, bool isStartingBattle)
        {
            AbilityData abilityData = new AbilityData(user, this, isStartingBattle, WorldManager.GetWorldState());
            switch (abilityData.GetWorldState)
            {
                case WorldManager.State.World:
                    worldTargetStrategy.StartTargeting(abilityData, 
                        () => AbilityActivated(abilityData),  () => TargetAquired(abilityData));
                    break;
                case WorldManager.State.Battle:
                    battleTargetStrategy.StartTargeting(abilityData, 
                        () => AbilityActivated(abilityData), () => TargetAquired(abilityData));
                    break;
                default:
                    //Need to find out if targeting is necessary
                    Debug.Log("No targeting found");
                    break; 
                    
            }
        }

        //Create a random ItemID if its empty
        public void OnBeforeSerialize()
        {
            // Generate and save a new UUID if this is blank.
            if (string.IsNullOrWhiteSpace(itemID))
            {
                itemID = System.Guid.NewGuid().ToString();
            }
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            // Require by the ISerializationCallbackReceiver but we don't need
            // to do anything with it.
        }

        //Forces a creature to perform an ability regardless if they have the resources
        public void ForceUseAbility(AbilityData abilityData)
        {
            AbilityActivated(abilityData);
            TargetAquired(abilityData);
        }

        //Perform effects onto targets
        public async void PerformEffects(AbilityData abilityData) {

            bool needsAttributeRollResult = false;
            if (abilityData.GetUser().GetComponent<Creature>())
                foreach (var effect in effectStrategies) {
                    if (effect is IStatAttributeReciver && ((IStatAttributeReciver)effect).UsesAttribute()) {

                        needsAttributeRollResult = true;
                        break;
                    }
                }

            uint attributeRollResult = 0;
            UIDiceMeter diceMeter = null;
            DiceRollerContainer diceRollContainer = null;

            //If the ability doesn't require any die rolls then skip it
            if (needsAttributeRollResult) {

                //Create the DiceMeter UI if player is in turn based combat
                if (abilityData.GetWorldState == WorldManager.State.Battle) {

                    diceMeter = Instantiate(Utility.PrefabHandler.GetPrefab(Utility.PrefabHandler.PrefabType.DiceMeter), abilityData.GetUser().transform.position, Quaternion.identity).GetComponent<UIDiceMeter>();
                    Time.timeScale = 0;
                }

                Task<DiceRollerContainer> taskdiceRollContainer = 
                    DiceTray.HandleAction(abilityData, GetAbilityAttributeUseadge, GetActionModifier, diceMeter);
                await taskdiceRollContainer;
                diceRollContainer = taskdiceRollContainer.Result;
                attributeRollResult = (uint)diceRollContainer.GetResult();
            }

            foreach (var effect in effectStrategies) {

                if (needsAttributeRollResult && effect is IStatAttributeReciver) {

                    ((IStatAttributeReciver)effect).AttributeEffect(abilityData, EffectFinished, 
                        attributeRollResult, useStat, diceRollContainer.GetFinalResultActions.ToArray());
                } else {

                    effect.StartEffect(abilityData, EffectFinished);
                }
            }

            if (diceMeter) {

                Time.timeScale = 1;
                Destroy(diceMeter.gameObject);

            }
        }

        private string[] GetActionTag() => new string[] { actionTags + GetDisplayName()};

        /// <summary>
        /// Once an ability is activated play this Action once
        /// </summary>
        private void AbilityActivated(AbilityData abilityData)
        {
            ActionHandeler actionHandler = abilityData.GetUser().GetComponent<ActionHandeler>();
            actionHandler?.AbilityActivated(abilityData);
            if (activatedAbilityAnimation == null) return;
            //play animation on user
            if(abilityData.GetWorldState == WorldManager.State.World) activatedAbilityAnimation.StartEffect(abilityData, null);
            
        }

        //Once targets has been found filter through and decide how to perform
        private void TargetAquired(AbilityData abilityData)
        {
            if (abilityData.GetTargets() == null && abilityData.GetLocation() == null) { 
                return; }

            foreach (FilterStrategy filter in filterStrategies) {
                abilityData.SetTargets(filter.Filter(abilityData, abilityData.GetTargets()));
            }

            if(abilityData.GetWorldState == WorldManager.State.World)
            {
                 
                PerformEffects(abilityData);

            } else {
                
                abilityData.GetUser().GetComponent<ActionHandeler>().StartPerformAbility(
                    new AbilityActionTask(abilityData));
            }
        }
          
        //When an effect finishes
        //Yet to be implemented
        private void EffectFinished()
        {

        }

        /// <summary>
        /// What attribute stat the ability is using and what defense it will target
        /// </summary>
        [System.Serializable]
        private struct AttributeVSStat {

            [SerializeField] Stat attribute;
            [SerializeField] Stat VSStat;


            public Stat GetVSStat => VSStat;

            public bool ChecKCompatebility(Stat attribute) => attribute == this.attribute;

        }
    }
}