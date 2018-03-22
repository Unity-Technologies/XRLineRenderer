using UnityEngine;

namespace Unity.Entities
{
    /// <summary>
    /// Regular bools are not blittable, which means we can't use them in component data
    /// This struct acts like a boolean while storing its value in a blittable-way
    /// </summary>
    public struct BlittableBool
    {
        short m_SavedValue;

        public bool Value
        {
            get { return m_SavedValue != 0; }
            set { m_SavedValue = (short)(value ? 1 : 0); }
        }
    }
}

