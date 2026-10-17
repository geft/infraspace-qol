using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace InfraSpaceQoL
{
    internal static class TerraformPatches
    {
        // Completion is shared by all terraforming types. Requiring near-total
        // completion prevents one early-game type from spreading across the map.
        private const float SaturatedProgress = 0.99f;
        private const int SaturatedAreaPercent = 95;

        public static void TerraformPrefix(ref Dictionary<long, Terraformer> ___terraformers,
            NativeArray<float> ___progress, int2 ___progressDim, float ___mapCorner,
            float ___mapSize, float ___worldSize, JobSequentialScheduler ___jobSequentialScheduler,
            out Dictionary<long, Terraformer> __state)
        {
            __state = null;
            if (___terraformers == null || ___terraformers.Count == 0 || !___progress.IsCreated)
            {
                return;
            }

            // Terrain/grass jobs share this buffer. Finish the scheduler chain
            // before reading it; the original method schedules the next write.
            ___jobSequentialScheduler.Complete();
            float[] progress = ___progress.ToArray();
            var expanded = new Dictionary<long, Terraformer>(___terraformers.Count);
            foreach (var pair in ___terraformers)
            {
                TerraformerInfo info = pair.Value.info;
                if (info.radius > 0f && info.efficiency > 0f)
                {
                    info.radius = GetExpandedRadius(progress, ___progressDim.x, ___progressDim.y,
                        ___worldSize, ___mapCorner, ___mapSize, info.position.x, info.position.z, info.radius);
                }
                expanded.Add(pair.Key, new Terraformer { info = info });
            }

            // Only the scheduled job's snapshot receives expanded coverage.
            // Restore real building radii even when the original method throws.
            __state = ___terraformers;
            ___terraformers = expanded;
        }

        public static Exception TerraformFinalizer(Exception __exception,
            ref Dictionary<long, Terraformer> ___terraformers, Dictionary<long, Terraformer> __state)
        {
            if (__state != null)
            {
                ___terraformers = __state;
            }
            return __exception;
        }

        internal static float GetExpandedRadius(float[] progress, int width, int height,
            float worldSize, float mapCorner, float mapSize, float x, float z, float nativeRadius)
        {
            if (nativeRadius <= 0f || progress == null || width <= 0 || height <= 0 ||
                (long)width * height != progress.Length || worldSize <= 1f || mapSize <= 0f)
            {
                return nativeRadius;
            }

            // Match TerraformerJob's cell coordinates and playable-area bounds.
            int minRow = ProjectCell(mapCorner, worldSize, width);
            int maxRow = ProjectCell(mapCorner + mapSize, worldSize, width);
            int minColumn = ProjectCell(mapCorner, worldSize, height);
            int maxColumn = ProjectCell(mapCorner + mapSize, worldSize, height);
            // Count concentric doubling bands in one pass. Accumulating them
            // below tests the whole circle at each stage, tolerating small holes
            // without letting a mostly unfinished outer ring expand again.
            var radii = new List<float> { nativeRadius };
            var cells = new List<int> { 0 };
            var saturated = new List<int> { 0 };
            for (int row = minRow; row <= maxRow; row++)
            {
                float dz = (float)row / width * worldSize - z;
                for (int column = minColumn; column <= maxColumn; column++)
                {
                    float dx = (float)column / height * worldSize - x;
                    float distanceSquared = dx * dx + dz * dz;
                    int band = 0;
                    while (distanceSquared > radii[band] * radii[band])
                    {
                        band++;
                        if (band == radii.Count)
                        {
                            radii.Add(radii[band - 1] * 2f);
                            cells.Add(0);
                            saturated.Add(0);
                        }
                    }
                    cells[band]++;
                    // Invalid progress counts against the saturation fraction.
                    if (progress[row * height + column] >= SaturatedProgress)
                    {
                        saturated[band]++;
                    }
                }
            }

            // Reconstruct stages from saved progress. Stop once coverage reaches
            // the playable map, or a circle has more than 5% unfinished cells.
            long totalCells = 0;
            long saturatedCells = 0;
            for (int stage = 0; stage < radii.Count - 1; stage++)
            {
                totalCells += cells[stage];
                saturatedCells += saturated[stage];
                if (totalCells == 0 || saturatedCells * 100L < totalCells * SaturatedAreaPercent)
                {
                    return radii[stage];
                }
            }
            return radii[radii.Count - 1];
        }

        private static int ProjectCell(float coordinate, float worldSize, int dimension)
        {
            return Helper.ProjectClampInt((int)coordinate, 0, (int)(worldSize - 1f), 0, dimension - 1);
        }
    }
}
