using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jellyfin.Models
{
    public class DiscoveredServer
    {
        public string Name { get; set; }
        public Guid Id { get; set; }
        public Uri Address { get; set; }
        public Uri EndpointAddress { get; set; }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var otherServer = (DiscoveredServer)obj;
            return this.Id == otherServer.Id;
        }
    }
}
