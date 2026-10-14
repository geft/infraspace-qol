using System;
using System.Collections.Generic;

namespace InfraSpaceQoL
{
    internal static class WorkforcePatches
    {
        internal sealed class IdleIndustry
        {
            internal Heap<FactoryProductionLogic> MinHeap;
            internal Heap<FactoryProductionLogic> MaxHeap;
            internal readonly List<FactoryProductionLogic> RemovedFromMin = new List<FactoryProductionLogic>();
            internal readonly List<FactoryProductionLogic> RemovedFromMax = new List<FactoryProductionLogic>();
        }

        internal sealed class EmploymentState
        {
            internal Dictionary<Resource, List<FactoryProductionLogic>> Industries;
            internal readonly List<IdleIndustry> IdleIndustries = new List<IdleIndustry>();
        }

        internal static bool ShouldReleaseWorkers(FactoryProductionLogic factory)
        {
            // Power, terraforming and spaceship work cannot be judged by stored goods.
            if (factory == null || factory.productionDefinition == null || factory.consumerProducer == null ||
                factory.logicOverride != null || factory.productionDefinition.powerOutput > 0f ||
                factory.GetMaxWorkers() <= 0)
            {
                return false;
            }

            List<ResourceCost> outputs = factory.productionDefinition.producables;
            int[] storage = factory.consumerProducer.outgoingStorage;
            if (outputs == null || outputs.Count == 0 || storage == null || storage.Length != outputs.Count)
            {
                return false;
            }
            foreach (ResourceCost output in outputs)
            {
                if (output.resource == null || output.resource.GetStackSize() <= 0)
                {
                    return false;
                }
            }
            // Native check requires EVERY output to reach its upgraded stack capacity.
            return factory.OutgoingStorageFull();
        }

        public static void CheckEmploymentPrefix(
            ref Dictionary<Resource, List<FactoryProductionLogic>> ___industries,
            Dictionary<Resource, Heap<FactoryProductionLogic>> ___industriesMinHeap,
            Dictionary<Resource, Heap<FactoryProductionLogic>> ___industriesMaxHeap,
            Dictionary<Resource, int> ___currentlyWorkingWorkersPerIndustry,
            List<Worker> ___unemployedWorkers,
            out EmploymentState __state)
        {
            __state = new EmploymentState { Industries = ___industries };
            Dictionary<Resource, List<FactoryProductionLogic>> activeIndustries = null;
            foreach (KeyValuePair<Resource, List<FactoryProductionLogic>> industry in ___industries)
            {
                List<FactoryProductionLogic> activeFactories = null;
                IdleIndustry idle = null;
                foreach (FactoryProductionLogic factory in industry.Value)
                {
                    if (!ShouldReleaseWorkers(factory))
                    {
                        continue;
                    }
                    if (idle == null)
                    {
                        idle = new IdleIndustry
                        {
                            MinHeap = ___industriesMinHeap[industry.Key],
                            MaxHeap = ___industriesMaxHeap[industry.Key]
                        };
                        __state.IdleIndustries.Add(idle);
                        activeFactories = new List<FactoryProductionLogic>(industry.Value);
                    }
                    activeFactories.Remove(factory);
                    // Keep worker ownership, unemployment and industry totals consistent.
                    // Do this within CheckEmployment, where the game already moves workers.
                    while (factory.workers.Count > 0)
                    {
                        ___unemployedWorkers.Add(factory.RemoveWorker());
                        ___currentlyWorkingWorkersPerIndustry[industry.Key]--;
                    }
                    if (idle.MinHeap.list.Remove(factory))
                    {
                        idle.RemovedFromMin.Add(factory);
                    }
                    if (idle.MaxHeap.list.Remove(factory))
                    {
                        idle.RemovedFromMax.Add(factory);
                    }
                }
                if (idle == null)
                {
                    continue;
                }
                idle.MinHeap.BuildComplete();
                idle.MaxHeap.BuildComplete();
                if (activeIndustries == null)
                {
                    activeIndustries = new Dictionary<Resource, List<FactoryProductionLogic>>(___industries);
                }
                activeIndustries[industry.Key] = activeFactories;
            }
            if (activeIndustries != null)
            {
                // Native PreUpdate calculates demand from these active factories and the
                // player's CURRENT resource priorities. Never overwrite player settings,
                // factory capacity, production definitions, or the saved building registry.
                ___industries = activeIndustries;
            }
        }

        public static Exception CheckEmploymentFinalizer(Exception __exception,
            ref Dictionary<Resource, List<FactoryProductionLogic>> ___industries, EmploymentState __state)
        {
            if (__state != null)
            {
                ___industries = __state.Industries;
                foreach (IdleIndustry idle in __state.IdleIndustries)
                {
                    foreach (FactoryProductionLogic factory in idle.RemovedFromMin)
                    {
                        idle.MinHeap.BuildAdd(factory);
                    }
                    foreach (FactoryProductionLogic factory in idle.RemovedFromMax)
                    {
                        idle.MaxHeap.BuildAdd(factory);
                    }
                    // Active factories may have gained/lost workers during balancing.
                    idle.MinHeap.BuildComplete();
                    idle.MaxHeap.BuildComplete();
                }
            }
            return __exception;
        }
    }
}
