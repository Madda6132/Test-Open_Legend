using UnityEngine;
using RPG.Abilities.Affliction;

namespace RPG.Abilities
{
    [CreateAssetMenu(fileName = "Affliction", menuName = "Ability/New Affliction", order = 0)]
    public class AfflictionAbility : AbstractAfflictionBehavior {
        [Tooltip("Item name to be displayed in UI.")]
        [SerializeField] string displayName = null;
        [Tooltip("Item description to be displayed in UI.")]
        [SerializeField][TextArea] string description = null;
        [Tooltip("The UI icon to represent this item in the inventory.")]
        [SerializeField] Sprite icon = null;
        [SerializeField] AfflictionType type; 


        [Header("Building Blocks")]
        [Tooltip("When a creature is afflicted while the same is still active")]
        [SerializeField] AfflictionBuildingBlock[] duplicateAffliction;
        [Tooltip("When the affliction starts")]
        [SerializeField] AfflictionBuildingBlock[] startEventAffliction;
        [Tooltip("When the affliction ends")]
        [SerializeField] AfflictionBuildingBlock[] endEventAffliction;
        [Tooltip("When the monobehavior runs update")]
        [SerializeField] AfflictionBuildingBlock[] updateEventAffliction;

        public enum AfflictionType {
            Bane,
            Boon
        }

        public AfflictionBuildingBlock[] GetStartEventBlock => startEventAffliction;

        public override void DuplicateAffliction(AfflictionDataHandelar durationHandelar) {
            foreach (var dup in duplicateAffliction) {
                dup.StartUpBuildingBlock(durationHandelar);
            }
        }

        public override void StartOfAffliction(AfflictionDataHandelar durationHandelar) {
            foreach (var startEvent in startEventAffliction) {
                startEvent.StartUpBuildingBlock(durationHandelar);
            }
        }

        public override void EndOfAffliction(AfflictionDataHandelar durationHandelar) {
            foreach (var endEvent in endEventAffliction) {
                endEvent.CancelBuildingBlock(durationHandelar);
            }
            foreach (var startEvent in startEventAffliction) {
                startEvent.CancelBuildingBlock(durationHandelar);
            }
        }


        public override void UpdateReplacer(AfflictionDataHandelar durationHandelar) {
            foreach (var updateEvent in updateEventAffliction) {
                updateEvent.StartUpBuildingBlock(durationHandelar);
            }
        }



        public Sprite GetIcon() => icon;
        public string GetDiscription() => description;
        public string GetName() => displayName;
        public AfflictionType GetAfflictionType() => type;

        public Color GetAfflictionTypeColor() {

            switch (type) {
                case AfflictionType.Bane:

                    return Color.black;
                case AfflictionType.Boon:

                    return Color.yellow;
                default:

                    return Color.gray;
                    
            }

        }
         

    }

}
