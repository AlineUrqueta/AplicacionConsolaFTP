using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class FormatoJson
    {
        public string Subject { get; set; }
        public string From { get; set; }
        public TemplateJson Template { get; set; }
        public List<Recipient> Recipients { get; set; }
    }

    public class Recipient
    {
        public string To { get; set; }
    }
}
