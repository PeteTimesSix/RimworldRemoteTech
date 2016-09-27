﻿using System;
using System.Collections.Generic;

namespace RemoteExplosives {
	/**
	 * A ticking scheduler for things that require a tick only every so often.
	 * Distributes tick calls uniformely over multiple frames to reduce the workload.
	 */
	public class DistributedTickScheduler {
		private static DistributedTickScheduler instance;
		public static DistributedTickScheduler Instance {
			get { return instance ?? (instance = new DistributedTickScheduler()); }
		}

		private readonly List<ListTicker> tickers = new List<ListTicker>();
		private int lastProcessedTick = -1;

		private DistributedTickScheduler() {
		}

		public void Initialize(int currentTick) {
			tickers.Clear();
			lastProcessedTick = currentTick;
		}

		public void Tick(int currentTick) {
			if (lastProcessedTick < 0) throw new Exception("Ticking not initalized DistributedTickScheduler");
			lastProcessedTick = currentTick;
			for (var i = 0; i < tickers.Count; i++) {
				tickers[i].Tick(currentTick);
			}
		}

		public void RegisterTickability(Action callback, int tickInterval) {
			if (lastProcessedTick < 0) throw new Exception("Adding callback to not initialized DistributedTickScheduler");
			if (tickInterval < 1) throw new Exception("Invalid tick interval: " + tickInterval);
			GetTicker(tickInterval).Register(callback, lastProcessedTick);
		}

		public void UnregisterTickability(Action callback, int tickInterval) {
			GetTicker(tickInterval).Unregister(callback);
		}

		private ListTicker GetTicker(int interval) {
			for (int i = 0; i < tickers.Count; i++) {
				if (tickers[i].tickInterval == interval) return tickers[i];
			}
			var ticker = new ListTicker(interval); 
			tickers.Add(ticker);
			return ticker;
		}

		private class ListTicker {
			public readonly int tickInterval;
			private readonly List<Action> tickList = new List<Action>();
			private int currentIndex;
			private int nextCycleStart;

			public ListTicker(int tickInterval) {
				this.tickInterval = tickInterval;
			}

			public void Tick(int currentTick) {
				if (nextCycleStart <= currentTick) {
					currentIndex = 0;
					nextCycleStart = currentTick + tickInterval;
				}
				var numCallbacksThisTick = Math.Ceiling(tickList.Count/(float)tickInterval);
				while (numCallbacksThisTick>0) {
					if(currentIndex>=tickList.Count) break;
					tickList[currentIndex]();
					currentIndex++;
					numCallbacksThisTick--;
				}
			}

			public void Register(Action callback, int currentTick) {
				tickList.Add(callback);
			}

			public void Unregister(Action callback) {
				var success = tickList.Remove(callback);
				if(!success) throw new Exception("Tried to unregister non-registered tick callback with interval: "+tickInterval);
			}
		}
	}
}