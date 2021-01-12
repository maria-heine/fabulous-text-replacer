using System.Text;

namespace FabulousReplacer
{
    public class MultilineStringBuilder
    {
        private StringBuilder builder;

        public override string ToString()
        {
            return builder.ToString();
        }

        public MultilineStringBuilder()
        {
            builder = new StringBuilder();
        }

        public MultilineStringBuilder(string titleLine)
        {
            builder = new StringBuilder();
            builder.Append(titleLine);
            AddSeparator();
        }

        public void AddLine(string line)
        {
            builder.Append($"{line} \n");
        }

        public void AddLine(string[] elements)
        {
            builder.Append($"{string.Join("", elements)} \n");
        }

        public void AddSeparator()
        {
            AddLine("");
            AddLine($"--------------------------");
            AddLine("");
        }
    }
}