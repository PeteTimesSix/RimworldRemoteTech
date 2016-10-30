﻿using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RemoteExplosives {
	/* 
	 * A self-replicating Thing with a concentration property.
	 * Will spread in cardinal directions when the concentration is high enough, and loose concentration over time.
	 * See MoteProperties_GasCloud for settings.
	 */
	public class GasCloud : Thing {
		private const float AlphaEasingDivider = 10f;
		private const float SpreadingAnimationDuration = 1f;

		private const string ConcentrationLabelId = "GasCloud_concentration_label";

		public delegate bool TraversibilityTest(Building b, GasCloud g);
		public static readonly Dictionary<Type, TraversibilityTest> TraversibleBuildings = new Dictionary<Type, TraversibilityTest> {
			{typeof(Building_Vent), (d,g)=> true },
			{typeof(Building_Door), (d,g)=> ((Building_Door)d).Open }
		};

		private static int GlobalOffsetCounter;
		private static readonly List<GasCloud> adjacentBuffer = new List<GasCloud>(4);
		private static readonly List<IntVec3> positionBuffer = new List<IntVec3>(4);

		public Vector2 spriteOffset;
		public Vector2 spriteScaleMultiplier = new Vector2(1f, 1f);
		public float spriteAlpha = 1f;
		public float spriteRotation;
		public int relativeZOrder; // to avoid z fighting among clouds
		private MoteProperties_GasCloud gasProps;
		
		private float interpolatedAlpha;
		private readonly InterpolatedValue interpolatedOffsetX;
		private readonly InterpolatedValue interpolatedOffsetY;
		private readonly InterpolatedValue interpolatedScale;
		private readonly InterpolatedValue interpolatedRotation;

		//saved fields
		private float concentration;
		private int gasTicksProcessed;
		//

		public float Concentration {
			get { return concentration; }
		}

		public bool IsBlocked {
			get {
				return !TileIsGasTraversible(Position, this);
			}
		}

		public GasCloud() {
			interpolatedOffsetX = new InterpolatedValue();
			interpolatedOffsetY = new InterpolatedValue();
			interpolatedScale = new InterpolatedValue();
			interpolatedRotation = new InterpolatedValue();
		}

		public override void SpawnSetup() {
			base.SpawnSetup();
			gasProps = def.mote as MoteProperties_GasCloud;
			relativeZOrder = ++GlobalOffsetCounter % 80;
			if (gasProps == null) throw new Exception("Missing required gas mote properties in " + def.defName);
			interpolatedScale.value = GetRandomGasScale();
			interpolatedRotation.value = GetRandomGasRotation();
			// uniformely distribute gas ticks to reduce per frame workload
			DistributedTickScheduler.Instance.RegisterTickability(GasTick, gasProps.GastickInterval);
		}

		public override void DeSpawn() {
			base.DeSpawn();
			DistributedTickScheduler.Instance.UnregisterTickability(GasTick, gasProps.GastickInterval);
		}

		public override void ExposeData() {
			base.ExposeData();
			Scribe_Values.LookValue(ref concentration, "concentration", 0);
			Scribe_Values.LookValue(ref gasTicksProcessed, "ticks", 0);
		}

		public override void Draw() {
			if (!Find.TickManager.Paused) {
				UpdateInterpolatedValues();
			}
			var targetApha = Mathf.Min(1f, concentration / gasProps.FullAlphaConcentration);
			spriteAlpha = interpolatedAlpha = DoAdditiveEasing(interpolatedAlpha, targetApha, AlphaEasingDivider, Time.deltaTime);
			spriteOffset = new Vector2(interpolatedOffsetX, interpolatedOffsetY);
			spriteScaleMultiplier = new Vector2(interpolatedScale, interpolatedScale);
			spriteRotation = interpolatedRotation;
			base.Draw();
		}

		private void UpdateInterpolatedValues() {
			interpolatedOffsetX.Update();
			interpolatedOffsetY.Update();
			if (gasProps.AnimationAmplitude > 0) {
				interpolatedScale.Update();
				interpolatedRotation.Update();
				if (interpolatedOffsetX.finished) {
					// start offset interpolation
					var newX = Rand.Range(-gasProps.AnimationAmplitude, gasProps.AnimationAmplitude);
					var newY = Rand.Range(-gasProps.AnimationAmplitude, gasProps.AnimationAmplitude);
					var duration = gasProps.AnimationPeriod.RandomInRange;
					interpolatedOffsetX.StartInterpolation(newX, duration, InterpolationCurves.CubicEaseInOut);
					interpolatedOffsetY.StartInterpolation(newY, duration, InterpolationCurves.CubicEaseInOut);
				}
				if (interpolatedScale.finished) {
					// start scale interpolation
					interpolatedScale.StartInterpolation(GetRandomGasScale(), gasProps.AnimationPeriod.RandomInRange, InterpolationCurves.CubicEaseInOut);
				}
				if (interpolatedRotation.finished) {
					// start rotation interpolation
					const float MaxRotationDelta = 90f;
					var newRotation = interpolatedRotation.value + Rand.Range(-MaxRotationDelta, MaxRotationDelta)*gasProps.AnimationAmplitude;
					interpolatedRotation.StartInterpolation(newRotation, gasProps.AnimationPeriod.RandomInRange, InterpolationCurves.CubicEaseInOut);
				}
			}
		}

		public override string GetInspectString() {
			return string.Format(ConcentrationLabelId.Translate(), string.Format("{0:n0}", concentration));
		}
		
		public void ReceiveConcentration(float amount) {
			concentration += amount;
			if (concentration < 0) concentration = 0;
		}

		public void BeginSpreadingTransition(GasCloud parentCloud, IntVec3 targetPosition) {
			interpolatedOffsetX.value = parentCloud.Position.x - targetPosition.x;
			interpolatedOffsetY.value = parentCloud.Position.z - targetPosition.z;
			interpolatedOffsetX.StartInterpolation(0, SpreadingAnimationDuration, InterpolationCurves.QuinticEaseOut);
			interpolatedOffsetY.StartInterpolation(0, SpreadingAnimationDuration, InterpolationCurves.QuinticEaseOut);
		}

		protected virtual void GasTick() {
			gasTicksProcessed++;
			// dissipate
			var underRoof = Find.RoofGrid.Roofed(Position);
			concentration -= underRoof ? gasProps.RoofedDissipation : gasProps.UnroofedDissipation;
			if(concentration<=0) {
				Destroy(DestroyMode.Kill);
				return;
			}
			
			//spread
			var gasTickFitForSpreading = gasTicksProcessed % gasProps.SpreadInterval == 0;
			if(gasTickFitForSpreading) {
				TryCreateNewNeighbours();
			}

			// if filled in
			if(IsBlocked) {
				ForcePushConcentrationToNeighbours();
			}

			// balance concentration
			ShareConcentrationWithMinorNeighbours();
		}

		private float GetRandomGasScale() {
			return 1f + Rand.Range(-gasProps.AnimationAmplitude, gasProps.AnimationAmplitude);
		}

		private float GetRandomGasRotation() {
			return Rand.Value * 360f;
		}

		// this is just a "current + difference / divider", but adjusted for frame rate
		private float DoAdditiveEasing(float currentValue, float targetValue, float easingDivider, float frameDeltaTime) {
			const float nominalFramerate = 60f;
			var dividerMultiplier = frameDeltaTime == 0 ? 0 : (1f / nominalFramerate) / frameDeltaTime;
			easingDivider *= dividerMultiplier;
			if (easingDivider < 1) easingDivider = 1;
			var easingStep = (targetValue - currentValue) / easingDivider;
			return currentValue + easingStep;
		}

		private List<IntVec3> GetSpreadableAdacentCells() {
			positionBuffer.Clear();
			for (int i = 0; i < 4; i++) {
				var adjPosition = GenAdj.CardinalDirections[i] + Position;
				if(TileIsGasTraversible(adjPosition, this) && Find.ThingGrid.ThingAt<GasCloud>(adjPosition) == null) {
					positionBuffer.Add(adjPosition);
				}
			}
			positionBuffer.Shuffle();
			return positionBuffer;
		}

		private List<GasCloud> GetAdjacentGasClouds() {
			adjacentBuffer.Clear();
			for (int i = 0; i < 4; i++) {
				var adjPosition = GenAdj.CardinalDirections[i] + Position;
				var cloud = Find.ThingGrid.ThingAt<GasCloud>(adjPosition);
				if(cloud!=null) {
					adjacentBuffer.Add(cloud);
				}
			}
			return adjacentBuffer;
		}

		private void ShareConcentrationWithMinorNeighbours() {
			var neighbours = GetAdjacentGasClouds();
			var numSharingNeighbours = 0;
			for (int i = 0; i < neighbours.Count; i++) {
				var neighbour = neighbours[i];
				if (neighbour.Concentration >= concentration || neighbour.IsBlocked) {
					neighbours[i] = null;
				} else {
					numSharingNeighbours++;
				}
			}
			if (numSharingNeighbours > 0) {
				for (int i = 0; i < neighbours.Count; i++) {
					var neighbour = neighbours[i];
					if (neighbour == null) continue;
					var neighbourConcentration = neighbour.concentration > 0 ? neighbour.Concentration : 1;
					var amountToShare = ((concentration - neighbourConcentration)/(numSharingNeighbours+1))*gasProps.SpreadAmountMultiplier;
					neighbour.ReceiveConcentration(amountToShare);
					concentration -= amountToShare;
				}

			}
		}

		private void ForcePushConcentrationToNeighbours() {
			var neighbours = GetAdjacentGasClouds();
			for (int i = 0; i < neighbours.Count; i++) {
				var neighbour = neighbours[i];
				if (neighbour.IsBlocked) continue;
				var pushAmount = concentration/neighbours.Count;
				neighbour.ReceiveConcentration(pushAmount);
				concentration -= pushAmount;
			}
		}

		private void TryCreateNewNeighbours() {
			var spreadsLeft = Mathf.FloorToInt(concentration / gasProps.SpreadMinConcentration);
			if (spreadsLeft <= 0) return;
			var viableCells = GetSpreadableAdacentCells();
			for (int i = 0; i < viableCells.Count; i++) {
				if (spreadsLeft <= 0) break;
				var targetPosition = viableCells[i];
				var newCloud = (GasCloud)ThingMaker.MakeThing(def);
				newCloud.BeginSpreadingTransition(this, targetPosition);
				GenPlace.TryPlaceThing(newCloud, targetPosition, ThingPlaceMode.Direct);
				spreadsLeft--;
			}
		}

		private bool TileIsGasTraversible(IntVec3 pos, GasCloud sourceCloud) {
			if (!pos.InBounds()) return false;
			var edifice = Find.EdificeGrid[pos];
			var walkable = Find.PathGrid.WalkableFast(pos);
			TraversibilityTest travTest = null;
			if (edifice != null) TraversibleBuildings.TryGetValue(edifice.GetType(), out travTest);
			return (walkable && travTest == null) || (travTest != null && travTest(edifice, sourceCloud));
		}
	}
}
