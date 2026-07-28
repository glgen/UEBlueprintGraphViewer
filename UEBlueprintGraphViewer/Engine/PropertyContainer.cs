using CUE4Parse.UE4.Objects.UObject;

namespace UEBlueprintGraphViewer.Engine
{
    // Container for new and old property types (FProperty and UProperty)
    public class PropertyContainer
    {
        public readonly bool IsNew;
        public FProperty? New;
        public UProperty? Old;

        public PropertyContainer(FProperty prop)
        {
            IsNew = true;
            New = prop;
        }

        public PropertyContainer(UProperty prop)
        {
            IsNew = false;
            Old = prop;
        }

        public void Clear()
        {
            New = null;
            Old = null;
        }

        public string GetName()
        {
            return (IsNew ? New!.Name : Old!.Name).ToString();
        }
        
        // Get type as string without F- and U- prefix (e.x. "ObjectProperty")
        public string GetPropType()
        {
            return (IsNew ? New! : Old! as object).GetType().Name.Substring(1);
        }

        public EPropertyFlags GetFlags()
        {
            if (IsNew)
            {
                return New!.PropertyFlags;
            }
            else
            {
                return Old!.PropertyFlags;
            }
        }

    }
}
