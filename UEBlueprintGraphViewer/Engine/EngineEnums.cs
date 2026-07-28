namespace UEBlueprintGraphViewer.Engine
{
    public class EngineEnums
    {
        public enum EEdGraphPinDirection
        {
            EGPD_Input,
            EGPD_Output,
            EGPD_MAX,
        }

        public enum EPinContainerType : byte
        {
            None,
            Array,
            Set,
            Map
        };
    }
}
