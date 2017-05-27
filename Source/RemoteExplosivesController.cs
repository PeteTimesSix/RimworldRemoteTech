﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Harmony;
using HugsLib;
using HugsLib.Settings;
using HugsLib.Utils;
using RimWorld;
using UnityEngine;
using Verse;

namespace RemoteExplosives {
	/**
	 * The hub of the mod.
	 * Injects trader stock generators, generates recipe copies for the workbench and injects comps.
	 */
	public class RemoteExplosivesController : ModBase {
		private const int ComponentValueInSteel = 40;
		private const int ForbiddenTimeoutSettingDefault = 30;
		private const int ForbiddenTimeoutSettingIncrement = 5;

		public static RemoteExplosivesController Instance { get; private set; }

		private readonly MethodInfo objectCloneMethod = typeof (object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
		// ReSharper disable once ConvertToConstant.Local
		private readonly bool showDebugControls = false;
		private SettingHandle<bool> settingForbidReplaced;
		private SettingHandle<int> settingForbidTimeout;

		public override string ModIdentifier {
			get { return "RemoteExplosives"; }
		}

		public new ModLogger Logger {
			get { return base.Logger; }
		}

		public int BlueprintForbidDuration {
			get { return settingForbidReplaced ? settingForbidTimeout : 0; }
		}

		public RemoteExplosivesController() {
			Instance = this;
		}

		public override void DefsLoaded() {
			InjectTraderStocks();
			InjectSteelRecipeVariants();
			InjectVanillaExplosivesComps();
			GetSettingsHandles();
			PrepareReflection();
		}

		private void PrepareReflection() {
			if (AccessTools.Method(typeof(Pawn_HealthTracker), "MakeDowned").MethodMatchesSignature(typeof(void), typeof(Pawn_HealthTracker), typeof(DamageDef), typeof(HediffDef))) {
				Logger.Error("Could not reflect required members");
			}
		}

		private void GetSettingsHandles() {
			settingForbidReplaced = Settings.GetHandle("forbidReplaced", "Setting_forbidReplaced_label".Translate(), "Setting_forbidReplaced_desc".Translate(), true);
			settingForbidTimeout = Settings.GetHandle("forbidTimeout", "Setting_forbidTimeout_label".Translate(), "Setting_forbidTimeout_desc".Translate(), ForbiddenTimeoutSettingDefault, Validators.IntRangeValidator(0, 100000000));
			settingForbidTimeout.SpinnerIncrement = ForbiddenTimeoutSettingIncrement;
			settingForbidTimeout.VisibilityPredicate = () => settingForbidReplaced.Value;
		}

		public override void OnGUI() {
			if (showDebugControls) DrawDebugControls();
		}

		/**
		 * Injects StockGenerators into existing traders.
		 */
		private void InjectTraderStocks() {
			var allInjectors = DefDatabase<TraderStockInjectorDef>.AllDefs;
			var affectedTraders = new List<TraderKindDef>();
			foreach (var injectorDef in allInjectors) {
				if (injectorDef.traderDef == null || injectorDef.stockGenerators.Count == 0) continue;
				affectedTraders.Add(injectorDef.traderDef);
				foreach (var stockGenerator in injectorDef.stockGenerators) {
					injectorDef.traderDef.stockGenerators.Add(stockGenerator);
				}
			}
			if (affectedTraders.Count > 0) {
				Logger.Trace(string.Format("Injected stock generators for {0} traders", affectedTraders.Count));
			}

			// Unless all defs are reloaded, we no longer need the injector defs
			DefDatabase<TraderStockInjectorDef>.Clear();
		}

		/**
		 * Injects copies of explosives recipes, changing components into an equivalent amount of steel
		 */
		private void InjectSteelRecipeVariants() {
			int injectCount = 0;
			foreach (var explosiveRecipe in GetAllExplosivesRecipes().ToList()) {
				var variant = TryMakeRecipeVariantWithSteel(explosiveRecipe);
				if (variant != null) {
					DefDatabase<RecipeDef>.Add(variant);
					injectCount++;
				}
			}

			if (injectCount > 0) {
				Logger.Trace(string.Format("Injected {0} alternate explosives recipes.", injectCount));
			}
		}

		/**
		 * Add comps to vanilla IED's so that they can be triggered by the manual detonator
		 */
		private void InjectVanillaExplosivesComps() {
			var ieds = new[] {
				GetDefWithWarning("TrapIEDBomb"),
				GetDefWithWarning("TrapIEDIncendiary"),
				GetDefWithWarning("FirefoamPopper")
			};
			foreach (var thingDef in ieds) {
				if (thingDef == null) continue;
				thingDef.comps.Add(new CompProperties_WiredDetonationReceiver());
				thingDef.comps.Add(new CompProperties_AutoReplaceable());
			}

		}

		private ThingDef GetDefWithWarning(string defName) {
			var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
			if (def == null) Logger.Warning("Could not get ThingDef for Comp injection: " + defName);
			return def;
		}

		private IEnumerable<RecipeDef> GetAllExplosivesRecipes() {
			return DefDatabase<RecipeDef>.AllDefs.Where(d => {
				var product = d.products.FirstOrDefault();
				return product != null && product.thingDef != null && product.thingDef.thingCategories != null && product.thingDef.thingCategories.Contains(Resources.ThingCategory.Explosives);
			});
		}

		// Will return null if recipe requires no components
		private RecipeDef TryMakeRecipeVariantWithSteel(RecipeDef recipeOriginal) {
			var recipeCopy = (RecipeDef) objectCloneMethod.Invoke(recipeOriginal, null);
			recipeCopy.shortHash = 0;
			InjectedDefHasher.GiveShortHasToDef(recipeCopy, typeof(RecipeDef));
			recipeCopy.defName += RemoteExplosivesUtility.InjectedRecipeNameSuffix;

			var newFixedFilter = new ThingFilter();
			foreach (var allowedThingDef in recipeOriginal.fixedIngredientFilter.AllowedThingDefs) {
				if (allowedThingDef == ThingDefOf.Component) continue;
				newFixedFilter.SetAllow(allowedThingDef, true);
			}
			newFixedFilter.SetAllow(ThingDefOf.Steel, true);
			recipeCopy.fixedIngredientFilter = newFixedFilter;
			recipeCopy.defaultIngredientFilter = null;

			float numComponentsRequired = 0;
			var newIngredientList = new List<IngredientCount>(recipeOriginal.ingredients);
			foreach (var ingredientCount in newIngredientList) {
				if (ingredientCount.filter.Allows(ThingDefOf.Component)) {
					numComponentsRequired = ingredientCount.GetBaseCount();
					newIngredientList.Remove(ingredientCount);
					break;
				}
			}
			if (numComponentsRequired == 0) return null;

			var steelFilter = new ThingFilter();
			steelFilter.SetAllow(ThingDefOf.Steel, true);
			var steelIngredient = new IngredientCount {filter = steelFilter};
			steelIngredient.SetBaseCount(ComponentValueInSteel*numComponentsRequired);
			newIngredientList.Add(steelIngredient);
			recipeCopy.ingredients = newIngredientList;
			recipeCopy.ResolveReferences();
			return recipeCopy;
		}

		private void DrawDebugControls() {
			var map = Find.VisibleMap;
			if(map == null) return;
			if (Widgets.ButtonText(new Rect(10, 10, 50, 20), "Cloud")) {
				DebugTools.curTool = new DebugTool("GasCloud placer", () => {
					const float concentration = 10000000;
					var cell = UI.MouseCell();
					var cloud = map.thingGrid.ThingAt<GasCloud>(cell);
					if (cloud != null) {
						cloud.ReceiveConcentration(concentration);
					} else {
						cloud = (GasCloud) ThingMaker.MakeThing(Resources.Thing.Gas_Sleeping);
						cloud.ReceiveConcentration(concentration);
						GenPlace.TryPlaceThing(cloud, cell, map, ThingPlaceMode.Direct);
					}
				});
			}
			if (Widgets.ButtonText(new Rect(10, 30, 50, 20), "Spark")) {
				DebugTools.curTool = new DebugTool("Spark", () => {
					Resources.Effecter.SparkweedIgnite.Spawn().Trigger(new TargetInfo(UI.MouseCell(), map), null);
				});
			}
			if (Widgets.ButtonText(new Rect(10, 50, 50, 20), "Failure")) {
				DebugTools.curTool = new DebugTool("Failure", () => {
					Resources.Effecter.DetWireFailure.Spawn().Trigger(new TargetInfo(UI.MouseCell(), map), null);
				});
			}

		}
	}
}