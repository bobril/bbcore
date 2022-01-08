using Lib.Translation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Lib.Test;

public class MessageParserTests
{
    [Fact]
    public void ParamExtractionWorks()
    {
        var ast = new MessageParser().Parse(@"{gender_of_host, select,
                female {{
            	    num_guests, plural, offset:1
                    =0 {{host} does not give a party.}
                    =1 {{host} invites {guest} to her party.}
                    =2 {{host} invites {guest} and one other person to her party.}
                    other {{host} invites {guest} and # other people to her party.}
            	}}
                male {{
            	    num_guests, plural, offset:1
                    =0 {{host} does not give a party.}
                    =1 {{host} invites {guest} to his party.}
                    =2 {{host} invites {guest} and one other person to his party.}
                    other {{host} invites {guest} and # other people to his party.}
                }}
                other {{
            	    num_guests, plural, offset:1
                    =0 {{host} does not give a party.}
                    =1 {{host} invites {guest} to their party.}
                    =2 {{host} invites {guest} and one other person to their party.}
                    other {{host} invites {guest} and # other people to their party.}
            	}}
            }");
        var pars = new HashSet<string>();
        ast.GatherParams(pars);
        Assert.Equal(new List<string> { "gender_of_host", "guest", "host", "num_guests" }, pars.OrderBy(s => s));
    }
}