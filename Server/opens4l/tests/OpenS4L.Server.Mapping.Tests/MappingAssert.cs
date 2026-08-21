using System.Text.Json;
using Xunit;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Differential harness: runs the live ExpressMapper config and the new Mapperly
    /// mapper on identical source objects and asserts the serialized DTOs are identical.
    /// This is what proves the Mapperly migration produces identical results.
    /// </summary>
    public static class MappingAssert
    {
        private static readonly JsonSerializerOptions s_json = new()
        {
            // Match both engines' output: serialize every public property, PascalCase.
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        };

        /// <summary>
        /// Asserts that the ExpressMapper result and the Mapperly result serialize to
        /// byte-identical JSON for the given source object.
        /// </summary>
        public static void Equal<TDto>(
            object source,
            System.Func<object, object> expressMapper,
            System.Func<object, object> mapperly)
        {
            var legacy = expressMapper(source);
            var modern = mapperly(source);

            var legacyJson = JsonSerializer.Serialize(legacy, s_json);
            var modernJson = JsonSerializer.Serialize(modern, s_json);

            Assert.True(
                legacyJson == modernJson,
                $"{typeof(TDto).Name} mismatch.\n" +
                $"--- ExpressMapper ---\n{legacyJson}\n--- Mapperly ---\n{modernJson}");
        }
    }
}
