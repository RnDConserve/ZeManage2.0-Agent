using System.Text.Json;

namespace BIManage.Infrastructure.Api
{
    /// <summary>
    /// The caller's rights on a protection list, as returned at the top level of the
    /// by-model GET responses: <c>{ "success", "canCreate", "canUpdate", "data": [...] }</c>.
    /// Dialogs hide their create/edit actions when a flag is false.
    /// </summary>
    public sealed class ProtectionPermissions
    {
        public bool CanCreate { get; }
        public bool CanUpdate { get; }

        public ProtectionPermissions(bool canCreate, bool canUpdate)
        {
            CanCreate = canCreate;
            CanUpdate = canUpdate;
        }

        /// <summary>
        /// Reads canCreate/canUpdate from a response root. A missing flag counts as allowed
        /// so servers that don't send them keep today's behaviour. Returns null when the
        /// root is not an object.
        /// </summary>
        public static ProtectionPermissions? FromResponse(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object) return null;
            return new ProtectionPermissions(ReadFlag(root, "canCreate"), ReadFlag(root, "canUpdate"));
        }

        private static bool ReadFlag(JsonElement root, string name)
        {
            foreach (var prop in root.EnumerateObject())
            {
                if (!string.Equals(prop.Name, name, System.StringComparison.OrdinalIgnoreCase)) continue;
                if (prop.Value.ValueKind == JsonValueKind.False) return false;
                if (prop.Value.ValueKind == JsonValueKind.True) return true;
            }
            return true;
        }
    }
}
