using System;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;

namespace Unity.Events
{
	public class NextFrameEvent<T> where T : struct, IComponentData
	{
		private World world;
		private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;
		private EntityManager entityManager;
		private EntityArchetype eventEntityArchetype;
		private EntityQuery eventEntityQuery;
		private Action<T> OnEventAction;

		private EventTrigger eventCaller;
		private EntityCommandBuffer entityCommandBuffer;

		public NextFrameEvent (World world, Action<T> OnEventAction = null)
		{
			this.world = world;
			this.OnEventAction = OnEventAction;
			endSimulationEntityCommandBufferSystem = world.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem> ();
			entityManager = world.EntityManager;

			eventEntityArchetype = entityManager.CreateArchetype (typeof (T));
			eventEntityQuery = entityManager.CreateEntityQuery (typeof (T));
		}

		public EventTrigger GetEventTrigger ()
		{
			entityCommandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer ();
			eventCaller = new EventTrigger (eventEntityArchetype, entityCommandBuffer);
			return eventCaller;
		}

		public void CaptureEvents (JobHandle jobHandleWhereEventsWereScheduled, Action<T> OnEventAction = null)
		{
			endSimulationEntityCommandBufferSystem.AddJobHandleForProducer (jobHandleWhereEventsWereScheduled);
			eventCaller.Playback (endSimulationEntityCommandBufferSystem.CreateCommandBuffer (), eventEntityQuery, OnEventAction == null ? this.OnEventAction : OnEventAction);
		}



		public struct EventTrigger
		{
			private struct EventJob : IJobForEachWithEntity<T>
			{
				public EntityCommandBuffer.Concurrent entityCommandBufferConcurrent;
				public NativeList<T> nativeList;

				public void Execute (Entity entity, int index, ref T c0)
				{
					nativeList.Add (c0);
					entityCommandBufferConcurrent.DestroyEntity (index, entity);
				}
			}

			private EntityCommandBuffer.Concurrent entityCommandBufferConcurrent;
			private EntityArchetype entityArchetype;

			public EventTrigger (EntityArchetype entityArchetype, EntityCommandBuffer entityCommandBuffer)
			{
				this.entityArchetype = entityArchetype;
				entityCommandBufferConcurrent = entityCommandBuffer.ToConcurrent ();
			}

			public void TriggerEvent (int entityInQueryIndex)
			{
				entityCommandBufferConcurrent.CreateEntity (entityInQueryIndex, entityArchetype);
			}

			public void TriggerEvent (int entityInQueryIndex, T t)
			{
				Entity entity = entityCommandBufferConcurrent.CreateEntity (entityInQueryIndex, entityArchetype);
				entityCommandBufferConcurrent.SetComponent (entityInQueryIndex, entity, t);
			}


			public void Playback (EntityCommandBuffer destroyEntityCommandBuffer, EntityQuery eventEntityQuery, Action<T> OnEventAction)
			{
				if (eventEntityQuery.CalculateEntityCount () > 0)
				{
					NativeList<T> nativeList = new NativeList<T> (Allocator.TempJob);
					new EventJob
					{
						entityCommandBufferConcurrent = destroyEntityCommandBuffer.ToConcurrent (),
						nativeList = nativeList,
					}.Run (eventEntityQuery);

					foreach (T t in nativeList)
					{
						OnEventAction (t);
					}

					nativeList.Dispose ();
				}
			}

		}

	}

	public class SameFrameEvent<T> where T : struct, IComponentData
	{

		private World world;
		private EntityManager entityManager;
		private EntityArchetype eventEntityArchetype;
		private EntityQuery eventEntityQuery;
		private Action<T> OnEventAction;

		private EventTrigger eventCaller;
		private EntityCommandBuffer entityCommandBuffer;

		public SameFrameEvent (World world, Action<T> OnEventAction = null)
		{
			this.world = world;
			this.OnEventAction = OnEventAction;
			entityManager = world.EntityManager;

			eventEntityArchetype = entityManager.CreateArchetype (typeof (T));
			eventEntityQuery = entityManager.CreateEntityQuery (typeof (T));
		}

		public EventTrigger GetEventTrigger ()
		{
			eventCaller = new EventTrigger (eventEntityArchetype, out entityCommandBuffer);
			return eventCaller;
		}

		public void CaptureEvents (JobHandle jobHandleWhereEventsWereScheduled, Action<T> OnEventAction = null)
		{
			eventCaller.Playback (jobHandleWhereEventsWereScheduled, entityCommandBuffer, entityManager, eventEntityQuery, OnEventAction == null ? this.OnEventAction : OnEventAction);
		}



		public struct EventTrigger
		{
			private EntityCommandBuffer.Concurrent entityCommandBufferConcurrent;
			private EntityArchetype entityArchetype;

			public EventTrigger (EntityArchetype entityArchetype, out EntityCommandBuffer entityCommandBuffer)
			{
				this.entityArchetype = entityArchetype;
				entityCommandBuffer = new EntityCommandBuffer (Allocator.TempJob);
				entityCommandBufferConcurrent = entityCommandBuffer.ToConcurrent ();
			}

			public void TriggerEvent (int entityInQueryIndex)
			{
				entityCommandBufferConcurrent.CreateEntity (entityInQueryIndex, entityArchetype);
			}

			public void TriggerEvent (int entityInQueryIndex, T t)
			{
				Entity entity = entityCommandBufferConcurrent.CreateEntity (entityInQueryIndex, entityArchetype);
				entityCommandBufferConcurrent.SetComponent (entityInQueryIndex, entity, t);
			}


			public void Playback (JobHandle jobHandleWhereEventsWereScheduled, EntityCommandBuffer entityCommandBuffer, EntityManager EntityManager, EntityQuery eventEntityQuery, Action<T> OnEventAction)
			{
				jobHandleWhereEventsWereScheduled.Complete ();
				entityCommandBuffer.Playback (EntityManager);
				entityCommandBuffer.Dispose ();

				int entityCount = eventEntityQuery.CalculateEntityCount ();
				if (entityCount > 0)
				{
					NativeArray<T> nativeArray = eventEntityQuery.ToComponentDataArray<T> (Allocator.TempJob);
					foreach (T t in nativeArray)
					{
						OnEventAction (t);
					}
					nativeArray.Dispose ();
				}

				EntityManager.DestroyEntity (eventEntityQuery);
			}

		}

	}
}