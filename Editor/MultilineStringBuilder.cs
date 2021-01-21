using System.Text;

namespace FabulousReplacer
{
    public class MultilineStringBuilder
    {
        private StringBuilder builder;

        public int Length => builder.Length;

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
            if (builder.Length > 40000)
            {
                return;
            }

            builder.Append($"{line} \n");
        }

        public void AddLine(string[] elements)
        {
            AddLine($"{string.Join("", elements)}");
        }

        public void AddSeparator()
        {
            AddLine("");
            AddLine($"--------------------------");
            AddLine("");
        }
    }
}