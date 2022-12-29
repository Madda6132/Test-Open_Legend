using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Character;
using RPG;
using RPG.Abilities;
using RPG.Stats;
using RPG.Battle.UI;
using System.Threading.Tasks;



public static class DiceTray 
{

    /// <summary>
    /// Get the Result of the action.
    /// Feed the UIDiceMeter to add UI visual
    /// </summary>
    /// <param name="abilityData"> Contains ability information and the user </param>
    /// <param name="attributeStat"> The attribute stat used for this ability. Determines what and how many dice used </param>
    /// <param name="actionModifier"> Contains the information about the action performed (Usually used to get bonuses and rules that apply to this action). 
    /// Example its an attack, its using the fire element </param>
    /// <param name="uIDiceMeter"> The UI were the result and dice are displayed</param>
    /// <returns></returns>
    public async static Task<DiceRollerContainer> HandleAction(
        AbilityData abilityData, 
        Stat attributeStat,
        ActionModifier actionModifier,
        UIDiceMeter uIDiceMeter = null) {

        ActionKeyWords attributeKeyWord = ActionKeyWords.Attributes;

        foreach (ActionKeyWords aType in Enum.GetValues(typeof(ActionKeyWords))) {
            if (aType.ToString() == attributeStat.ToString()) attributeKeyWord = aType;
        }


        // 1. check the users modifiers that apply to the action with the keywords and tags
        ActionKeyWords startKeywords = actionModifier.GetKeyWords;
        ActionKeyWords getAttributs = startKeywords | attributeKeyWord;
        actionModifier.SetKeyWords(getAttributs);
        int attribute = (abilityData.GetUser().GetComponent<Stats>().GetStat(attributeStat) + 
            Utilitys.GetModifiers(abilityData.GetUser(), actionModifier));
        actionModifier.SetKeyWords(startKeywords);
        // 2. Check the user and opponents advantage modifier for the action with the keywords and tag. 
        //      (Opponents should add the keyword tootherCreatures)

        // 3. Get every event for the roll
        
        //Get any rules that effects the dice and result
        #region Get Action Events
        List<BaseActionRule> rollDiceEvents = new();
        List<BaseActionRule> confirmDiceEvents = new();
        List<BaseActionRule> sumResultEvents = new();
        List<BaseActionRule> endResultEvents = new();


        
        foreach (var actionEventComp in abilityData.GetUser().GetComponents<IActionEvent>()) {
            foreach (var actionEvent in actionEventComp.GetAdditionalEvent(abilityData, actionModifier)) {
                switch (actionEvent.actionPhase) {
                    case DiceRollingPhase.RollDice:
                        rollDiceEvents.Add(actionEvent.actionEvent);
                        break;
                    case DiceRollingPhase.ConfirmDice:
                        confirmDiceEvents.Add(actionEvent.actionEvent);
                        break;
                    case DiceRollingPhase.SumResult:
                        sumResultEvents.Add(actionEvent.actionEvent);
                        break;
                    case DiceRollingPhase.EndResult:
                        endResultEvents.Add(actionEvent.actionEvent);
                        break;
                    default:
                        break;
                }
            }


        }
        #endregion

        // 4. roll and gain the end result add any flat bonuses to the result
        //Create memory to keep track of dice, as to not repeat the effect on the same dice
        Dictionary<BaseActionRule, object> rollDiceMemory = new();
        rollDiceEvents.ForEach(addEvent => rollDiceMemory.Add(addEvent, null));

        Dictionary<BaseActionRule, object> confirmDiceMemory = new();
        confirmDiceEvents.ForEach(addEvent => confirmDiceMemory.Add(addEvent, null));

        Dictionary<BaseActionRule, object> sumResultMemory = new();
        sumResultEvents.ForEach(addEvent => sumResultMemory.Add(addEvent, null));

        DiceRollerContainer diceRollerContainer = new(abilityData, actionModifier, 
            AttributeDiceGetter.GetAttributeDice(attribute, uIDiceMeter));
        
        bool breakLoop = false; 
        while (!breakLoop) {

            diceRollerContainer.nextPhase = true;

            //If the dice are displayed on the UI it will pause phases for the player to observe the result
            switch (diceRollerContainer.GetPhase) {

            case DiceRollingPhase.RollDice:

                diceRollerContainer.GetAttributeDice.RollUnRolledDice();
                if(uIDiceMeter) await Task.Delay(400);

                //End of Phase
                Task<Dictionary<BaseActionRule, object>> eventMemory = rollDiceEvents.
                        PerformAllRules(diceRollerContainer, rollDiceMemory, uIDiceMeter);

                await eventMemory;
                rollDiceMemory = eventMemory.Result;

                 if (uIDiceMeter) await Task.Delay(200);

                if(diceRollerContainer.nextPhase) diceRollerContainer.SetPhase(DiceRollingPhase.ConfirmDice);
                break;

            //Handles dice memory for rules such as advantages and disadvantages
            case DiceRollingPhase.ConfirmDice:

                //End of Phase
                eventMemory = confirmDiceEvents.PerformAllRules(diceRollerContainer, confirmDiceMemory, uIDiceMeter);
                await eventMemory;
                confirmDiceMemory = eventMemory.Result;


                    if (uIDiceMeter) await Task.Delay(500);

                if (diceRollerContainer.nextPhase) diceRollerContainer.SetPhase(DiceRollingPhase.SumResult);
                break;

            //Adds all the die results and bonuses for the end result
            case DiceRollingPhase.SumResult:

                //End of Phase
                eventMemory = sumResultEvents.PerformAllRules(diceRollerContainer, sumResultMemory, uIDiceMeter);
                await eventMemory;
                sumResultMemory = eventMemory.Result;

                if (diceRollerContainer.nextPhase) breakLoop = true;
                break;

            default:
                break;
            }
        }

        
        if (uIDiceMeter) {
             
            await uIDiceMeter.FinsihAndAddToResult(); 
            await Task.Delay(200);
        }

        // 5. Check the events for the result of the roll
        endResultEvents.ForEach(x => x.ActivateEvent(diceRollerContainer, null));

        // 6. Finish the action
        return diceRollerContainer;
    }

    private static async Task<Dictionary<BaseActionRule, object>> PerformAllRules(this List<BaseActionRule> rules, 
        DiceRollerContainer diceRollerContainer, Dictionary<BaseActionRule, object> memory, UIDiceMeter uIDiceMeter = null) {

        List<Task<(BaseActionRule, object)>> allTasks = new(); 

        foreach (var rule in rules) {
            allTasks.Add( rule.ActivateEvent(diceRollerContainer, memory[rule], uIDiceMeter));
        }

        if(0 < allTasks.Count)
        await Task.WhenAll(allTasks);

        Dictionary<BaseActionRule, object> newMemorys = new();
        allTasks.ForEach(task => { 
            
            (BaseActionRule rule, object memory) result = task.Result;
            newMemorys.Add(result.rule, result.memory);
        
        });

        return newMemorys;
    }
}