using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Root;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Class;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Class.Mechanic;
using Kingmaker.UI.MVVM._VM.Other.NestedSelectionGroup;
using Kingmaker.UI.MVVM._VM.ServiceWindows.CharacterInfo.Sections.LevelClassScores.Classes;
using Kingmaker.UI.MVVM._VM.ServiceWindows.CharacterInfo.Sections.Progression.ChupaChupses;
using Kingmaker.UI.MVVM._VM.ServiceWindows.CharacterInfo.Sections.Progression.Main;
using Kingmaker.UI.MVVM._VM.ServiceWindows.CharacterInfo.Sections.Progression.Spellbook;
using Kingmaker.UI.MVVM._VM.Tooltip.Templates;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;
using Kingmaker.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;
using UnityModManagerNet;
using static UnityModManagerNet.UnityModManager;

namespace MultipleArchetypes {
    static class Main {

        public static Settings Settings;
        public static bool Enabled;
        public static ModEntry Mod;
        public static void Log(string msg) {
            Mod.Logger.Log(msg);
        }
        [System.Diagnostics.Conditional("DEBUG")]
        public static void LogDebug(string msg) {
            Mod.Logger.Log(msg);
        }

        static bool Load(UnityModManager.ModEntry modEntry) {
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll();
            Mod = modEntry;
            Settings = Settings.Load<Settings>(modEntry);
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry) {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("MultiArchetype", GUILayout.ExpandWidth(false))) {
                Settings.MultiArchetype = !Settings.MultiArchetype;
            }
            GUILayout.Label(
                string.Format($"{Settings.MultiArchetype}"), GUILayout.ExpandWidth(false)
            );
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry) {
            Settings.Save(modEntry);
        }

        [HarmonyPatch(typeof(ClassData), nameof(ClassData.CalcSkillPoints))]
        static class ClassData_CalcSkillPoints_Patch {
            static bool Prefix(ClassData __instance, ref int __result) {
                if (!__instance.Archetypes.Any()) { return true; }
                __result = __instance.CharacterClass.SkillPoints + __instance.Archetypes.Select((BlueprintArchetype a) => a.AddSkillPoints).Max();
                return false;
            }
        }
        [HarmonyPatch(typeof(TooltipTemplateClass), MethodType.Constructor, new Type[] { typeof(ClassData) })]
        static class TooltipTemplateClass_Constructor_Patch {
            static void Postfix(ref TooltipTemplateClass __instance, ClassData classData) {
                string Name = string.Join("/", classData.Archetypes.Select(a => a.Name));
                string Desc = string.Join("\n\n", classData.Archetypes.Select(a => a.Description));
                if (!string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(Name)) {
                    var NameSetter = AccessTools.Field(typeof(TooltipTemplateClass), "m_Name");
                    var DescSetter = AccessTools.Field(typeof(TooltipTemplateClass), "m_Desc");
                    NameSetter.SetValue(__instance, Name);
                    DescSetter.SetValue(__instance, Desc);
                }
            }
        }
        [HarmonyPatch(typeof(CharInfoClassEntryVM), MethodType.Constructor, new Type[] { typeof(ClassData) })]
        static class CharInfoClassEntryVM_Constructor_Patch {
            static void Postfix(CharInfoClassEntryVM __instance, ClassData classData) {
                string Name = string.Join("\n", classData.Archetypes.Select(a => a.Name));
                if (!string.IsNullOrEmpty(Name)) {
                    var ClassName = AccessTools.Field(typeof(CharInfoClassEntryVM), "<ClassName>k__BackingField");
                    ClassName.SetValue(__instance, Name);
                }
            }
        }
        [HarmonyPatch(typeof(ClassProgressionVM), MethodType.Constructor, new Type[] { typeof(UnitDescriptor), typeof(ClassData) })]
        static class ClassProgressionVM_Constructor_Patch {
            static void Postfix(ClassProgressionVM __instance, UnitDescriptor unit, ClassData unitClass) {
                string Name = string.Join("/", unitClass.Archetypes.Select(a => a.Name));
                if (!string.IsNullOrEmpty(Name)) {
                    __instance.Name = string.Join(" ", unitClass.CharacterClass.Name, $"({Name})");
                }
                var castingArchetype = unitClass.Archetypes.Where(a => a.ReplaceSpellbook != null).FirstOrDefault();
                if (castingArchetype != null) {
                    __instance.AddDisposable(__instance.SpellbookProgressionVM = new SpellbookProgressionVM(
                        __instance.m_UnitClass, 
                        castingArchetype, 
                        __instance.m_Unit, 
                        __instance.m_LevelProgressionVM));
                }
            }
        }
        [HarmonyPatch(typeof(CharGenClassSelectorItemVM), MethodType.Constructor, new Type[] {
            typeof(BlueprintCharacterClass),
            typeof(BlueprintArchetype),
            typeof(LevelUpController),
            typeof(INestedListSource),
            typeof(ReactiveProperty<CharGenClassSelectorItemVM>),
            typeof(ReactiveProperty<TooltipBaseTemplate>),
            typeof(bool),
            typeof(bool),
            typeof(bool),
        })]
        static class CharGenClassSelectorItemVM_Constructor_Patch {
            static void Postfix(CharGenClassSelectorItemVM __instance,
                BlueprintCharacterClass cls,
                BlueprintArchetype archetype,
                LevelUpController levelUpController,
                INestedListSource source,
                ReactiveProperty<CharGenClassSelectorItemVM> selectedArchetype,
                ReactiveProperty<TooltipBaseTemplate> tooltipTemplate,
                bool prerequisitesDone,
                bool canSelect,
                bool allowSwitchOff) {

                if (__instance.HasClassLevel) {
                    ClassData classData = levelUpController.Unit.Progression.GetClassData(cls);
                    if (!classData.Archetypes.Any()) { return; }
                    var Name = string.Join("/", classData.Archetypes.Select(a => a.Name));
                    var DisplayName = AccessTools.Field(typeof(CharGenClassSelectorItemVM), "DisplayName");
                    DisplayName.SetValue(__instance, $"{cls.Name} — {Name}");
                }
            }
        }
        [HarmonyPatch(typeof(CharGenClassSelectorItemVM), nameof(CharGenClassSelectorItemVM.GetArchetypesList), new Type[] { typeof(BlueprintCharacterClass) })]
        static class CharGenClassSelectorItemVM_GetArchetypesList_Patch {
            public static List<NestedSelectionGroupEntityVM> archetypes;
            static void Postfix(CharGenClassSelectorItemVM __instance, List<NestedSelectionGroupEntityVM> __result) {
                archetypes = __result;
            }
        }
        [HarmonyPatch(typeof(NestedSelectionGroupEntityVM), nameof(NestedSelectionGroupEntityVM.SetSelected), new Type[] { typeof(bool) })]
        static class NestedSelectionGroupEntityVM_SetSelected_Patch {
            static bool Prefix(NestedSelectionGroupEntityVM __instance, ref bool state) {
                if (!Settings.MultiArchetype) { return true; }
                var VM = __instance as CharGenClassSelectorItemVM;
                var controller = Game.Instance?.LevelUpController;
                if (VM == null || controller == null) { return true; }
                var progression = controller.Preview?.Progression;
                var classData = controller?.Preview?.Progression?.GetClassData(controller.State.SelectedClass);
                if (classData == null) { return true; }
                if (controller.Unit.Progression.GetClassLevel(VM.Class) >= 1) { return true; }
                var hasArchetype = classData.Archetypes.HasItem(VM.Archetype);
                state |= hasArchetype;
                if (!state) {
                    if (progression != null && VM.Archetype != null) {
                        VM.SetAvailableState(progression.CanAddArchetype(classData.CharacterClass, VM.Archetype) && VM.PrerequisitesDone);
                    }
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(NestedSelectionGroupEntityVM), nameof(NestedSelectionGroupEntityVM.SetSelectedFromView), new Type[] { typeof(bool) })]
        static class NestedSelectionGroupEntityVM_SetSelectedFromView_Patch {
            static bool Prefix(NestedSelectionGroupEntityVM __instance, bool state) {
                if (!Settings.MultiArchetype) { return true; }
                if (!state && !__instance.AllowSwitchOff) {
                    return false;
                }
                __instance.IsSelected.Value = state;
                __instance.RefreshView.Execute();
                if (state) {
                    __instance.DoSelectMe();
                }
                //__instance.SetSelected(state);
                return false;
            }
        }
        [HarmonyPatch(typeof(CharGenClassPhaseVM), nameof(CharGenClassPhaseVM.OnSelectorArchetypeChanged), new Type[] { typeof(BlueprintArchetype) })]
        static class CharGenClassPhaseVM_OnSelectorArchetypeChanged_Patch {
            static bool Prefix(CharGenClassPhaseVM __instance, BlueprintArchetype archetype) {
                if (!Settings.MultiArchetype) { return true; }
                __instance.UpdateTooltipTemplate(false);
                if (__instance.LevelUpController.State.SelectedClass == null) {
                    return false;
                }
                UnitProgressionData Progression = __instance.LevelUpController.Preview.Progression;
                ClassData classData = __instance.LevelUpController.Preview
                    .Progression.GetClassData(__instance.LevelUpController.State.SelectedClass);

                if (classData != null && archetype != null ? !Progression.CanAddArchetype(classData.CharacterClass, archetype) : true) {
                    classData.Archetypes.ForEach(delegate (BlueprintArchetype a) {
                        __instance.LevelUpController.RemoveArchetype(a);
                    });
                }
                __instance.LevelUpController.RemoveArchetype(archetype);
                if (archetype != null && !__instance.LevelUpController.AddArchetype(archetype)) {
                    MainThreadDispatcher.Post(delegate (object _) {
                        __instance.SelectedArchetypeVM.Value = null;
                    }, null);
                }
                __instance.UpdateClassInformation();
                return false;
            }
        }
        [HarmonyPatch(typeof(ClassProgressionVM), MethodType.Constructor, new Type[] {
            typeof(UnitDescriptor),
            typeof(BlueprintCharacterClass),
            typeof(BlueprintArchetype),
            typeof(bool),
            typeof(int),
        })]
        static class ClassProgressionVM2_Constructor_Patch {
            static void Postfix(ClassProgressionVM __instance, BlueprintCharacterClass classBlueprint, int level, bool buildDifference) {
                var data = __instance.ProgressionVms.Select(vm => vm.ProgressionData).OfType<AdvancedProgressionData>().First();
                __instance.ProgressionVms.Clear();
                var addArchetypes = Game.Instance.LevelUpController.LevelUpActions.OfType<AddArchetype>();
                foreach (var add in addArchetypes) {
                    data.AddArchetype(add.Archetype);
                }
                var newVM = new ProgressionVM(data, __instance.m_Unit, new int?(level), buildDifference);
                __instance.ProgressionVms.Add(newVM);
                __instance.AddProgressions(__instance.m_Unit.Progression.GetClassProgressions(__instance.m_UnitClass).EmptyIfNull<ProgressionData>());
                __instance.AddProgressionSources(newVM.ProgressionSourceFeatures);
                var archetypeString = string.Join("/", addArchetypes.Select(a => a.Archetype.Name));
                if (!string.IsNullOrEmpty(archetypeString)) {
                    __instance.Name = string.Join(" ", classBlueprint.Name, $"({archetypeString})");
                }
                var castingArchetype = addArchetypes.Select(a => a.Archetype).Where(a => a.ReplaceSpellbook != null).FirstOrDefault();
                if (castingArchetype != null) {
                    __instance.AddDisposable(__instance.SpellbookProgressionVM = new SpellbookProgressionVM(
                        __instance.m_UnitClass,
                        castingArchetype,
                        __instance.m_Unit,
                        __instance.m_LevelProgressionVM));
                }
            }
        }
        [HarmonyPatch(typeof(ProgressionVM), nameof(ProgressionVM.SetClassArchetypeDifType), new Type[] { typeof(ProgressionVM.FeatureEntry) })]
        static class ProgressionVM_SetClassArchetypeDifType_Patch {
            static void Postfix(ProgressionVM __instance, ref ProgressionVM.FeatureEntry featureEntry) {
                var featureEntry2 = featureEntry;
                foreach (var archetype in __instance.ProgressionData.Archetypes) {
                    foreach (var removeFeature in archetype.RemoveFeatures.Where(entry => entry.Level == featureEntry2.Level)) {
                        if (removeFeature.Features.Any(f => f == featureEntry2.Feature)) {
                            featureEntry.DifType = ClassArchetypeDifType.Removed;
                        };
                    }
                    foreach (var addFeature in archetype.AddFeatures.Where(entry => entry.Level == featureEntry2.Level)) {
                        if (addFeature.Features.Any(f => f == featureEntry2.Feature)) {
                            featureEntry.DifType = ClassArchetypeDifType.Added;
                        };
                    }
                }
            }
        }
        //Details Tab in CharGen
        [HarmonyPatch(typeof(CharGenClassCasterStatsVM), MethodType.Constructor, new Type[]{ typeof(BlueprintCharacterClass), typeof(BlueprintArchetype) })]
        static class CharGenClassCasterStatsVM_MultiArchetype_Patch {
            static void Postfix(CharGenClassCasterStatsVM __instance, BlueprintCharacterClass valueClass, BlueprintArchetype valueArchetype) {
                var controller = Game.Instance?.LevelUpController;
                if (controller == null) { return; }
                var classData = controller.Preview?.Progression?.GetClassData(valueClass);
                if (classData == null) { return; }
                __instance.CanCast.Value = classData.Spellbook != null;
                if (classData.Spellbook == null) { return; }
                var changeTypeArchetype = classData.Archetypes?.Where(a => a.ChangeCasterType).FirstOrDefault();
                __instance.MaxSpellsLevel.Value = classData.Spellbook.MaxSpellLevel.ToString();
                __instance.CasterAbilityScore.Value = LocalizedTexts.Instance.Stats.GetText(classData.Spellbook.CastingAttribute);
                __instance.CasterMindType.Value = ((changeTypeArchetype == null) ? 
                    (UIUtilityUnit.GetCasterMindType(valueClass) ?? "—") : (UIUtilityUnit.GetCasterMindType(changeTypeArchetype) ?? "—"));
                __instance.SpellbookUseType.Value = UIUtilityUnit.GetCasterSpellbookUseType(classData.Spellbook);
            }
        }
        //Details Tab in CharGen
        [HarmonyPatch(typeof(CharGenClassMartialStatsVM), MethodType.Constructor, new Type[] { typeof(BlueprintCharacterClass), typeof(BlueprintArchetype), typeof(UnitDescriptor) })]
        static class CharGenClassMartialStatsVM_MultiArchetype_Patch {
            static void Postfix(CharGenClassMartialStatsVM __instance, BlueprintCharacterClass valueClass, BlueprintArchetype valueArchetype, UnitDescriptor unit) {
                Main.Log("CharGenClassMartialStatsVM::Triggered");
                var controller = Game.Instance?.LevelUpController;
                if (controller == null) { return; }
                var classData = controller.Preview?.Progression?.GetClassData(valueClass);
                if (classData == null) { return; }
                Main.Log("Made it to override");
                __instance.Fortitude.Value = UIUtilityUnit.GetStatProgressionGrade(classData.FortitudeSave);
                __instance.Will.Value = UIUtilityUnit.GetStatProgressionGrade(classData.WillSave);
                __instance.Reflex.Value = UIUtilityUnit.GetStatProgressionGrade(classData.ReflexSave);
                __instance.BAB.Value = UIUtilityUnit.GetStatProgressionGrade(classData.BaseAttackBonus);
            }
        }
        //Details Tab in CharGen
        [HarmonyPatch(typeof(CharGenClassSkillsVM), MethodType.Constructor, new Type[] { typeof(BlueprintCharacterClass), typeof(BlueprintArchetype) })]
        static class CharGenClassSkillsVM_MultiArchetype_Patch {
            static void Postfix(CharGenClassSkillsVM __instance, BlueprintCharacterClass valueClass, BlueprintArchetype valueArchetype) {
                Main.Log("CharGenClassSkillsVM::Triggered");
                var controller = Game.Instance?.LevelUpController;
                if (controller == null) { return; }
                var classData = controller.Preview?.Progression?.GetClassData(valueClass);
                if (classData == null) { return; }
                Main.Log("Made it to override");
                var classSkills = classData.Archetypes.SelectMany(a => a.ClassSkills)
                    .Concat(classData.CharacterClass.ClassSkills).Distinct().ToArray();
                __instance.ClassSkills.Clear();
                foreach (var skill in classSkills) {
                    CharGenClassStatEntryVM charGenClassStatEntryVM = new CharGenClassStatEntryVM(skill);
                    __instance.AddDisposable(charGenClassStatEntryVM);
                    __instance.ClassSkills.Add(charGenClassStatEntryVM);
                }
                return;
            }
        }
        //Details Tab in CharGen
        [HarmonyPatch(typeof(CharGenClassPhaseVM), nameof(CharGenClassPhaseVM.UpdateClassInformation))]
        static class CharGenClassPhaseVM_UpdateClassInformation_MultiArchetype_Patch {
            static void Postfix(CharGenClassPhaseVM __instance) {
                Main.Log("CharGenClassPhaseVM::UpdateClassInformation");
                var controller = Game.Instance?.LevelUpController;
                if (controller == null) { return; }
                var classData = controller.Preview?.Progression?.GetClassData(__instance.SelectedClassVM.Value?.Class);
                if (classData == null) { return; }
                Main.Log("Made it to override");
                var classSkills = classData.Archetypes.SelectMany(a => a.ClassSkills)
                    .Concat(classData.CharacterClass.ClassSkills).Distinct().ToArray();
                //this.SelectedClassVM.Value.Class.Name + " — " + this.SelectedArchetypeVM.Value.Archetype.Name;
                string ArchetypeName = string.Join("/", classData.Archetypes.Select(a => a.Name));
                if (!string.IsNullOrEmpty(ArchetypeName)) {
                    __instance.ClassDisplayName.Value = string.Join(" ", classData.CharacterClass.Name, $"({ArchetypeName})");
                }
                string ArchetypeDrescription = string.Join("\n\n", classData.Archetypes.Select(a => a.Description));
                if (!string.IsNullOrEmpty(ArchetypeName)) {
                    __instance.ClassDescription.Value = ArchetypeDrescription;
                }
                return;
            }
        }

        //ClassProgressionVM
        //base.AddDisposable(this.m_LevelProgressionVM = new LevelProgressionVM(this.Level));
		//base.AddDisposable(this.StatsProgressionVM = new StatsProgressionVM(this.m_UnitClass, this.m_UnitArchetype, this.m_Unit, this.m_LevelProgressionVM));
		//base.AddDisposable(this.SpellbookProgressionVM = new SpellbookProgressionVM(this.m_UnitClass, this.m_UnitArchetype, this.m_Unit, this.m_LevelProgressionVM));
    }
}