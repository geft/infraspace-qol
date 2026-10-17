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
            double nearestIncomplete = double.PositiveInfinity;
            double farthest = 0d;
            float nativeSquared = nativeRadius * nativeRadius;
            for (int row = minRow; row <= maxRow; row++)
            {
                float dz = (float)row / width * worldSize - z;
                for (int column = minColumn; column <= maxColumn; column++)
                {
                    float dx = (float)column / height * worldSize - x;
                    float distanceSquared = dx * dx + dz * dz;
                    farthest = Math.Max(farthest, distanceSquared);
                    // NaN is treated as incomplete, never as a license to expand.
                    if (!(progress[row * height + column] >= SaturatedProgress))
                    {
                        if (distanceSquared <= nativeSquared)
                        {
                            return nativeRadius;
                        }
                        nearestIncomplete = Math.Min(nearestIncomplete, distanceSquared);
                    }
                }
            }

            // Double only after the entire previous circle has saturated.
            // Reconstructing stages from saved progress avoids mod save data.
            double frontier = double.IsPositiveInfinity(nearestIncomplete) ? farthest : nearestIncomplete;
            double radius = nativeRadius;
            while (radius * radius < frontier)
            {
                radius *= 2d;
            }
            return (float)radius;
        }

        private static int ProjectCell(float coordinate, float worldSize, int dimension)
        {
            return Helper.ProjectClampInt((int)coordinate, 0, (int)(worldSize - 1f), 0, dimension - 1);
        }
    }
}
