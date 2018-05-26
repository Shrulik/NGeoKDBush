using System;
using System.Collections.Generic;
using Shrulik.NKDBush;

namespace Shrulik.NGeoKDBush
{
    public interface IGeoKDBush<T>
    {
        List<T> Around(IKDBush<T> index, double lng, double lat, int? maxResults = null,
            double? maxDistance = null, Predicate<T> predicate = null);

        double Distance(double lng, double lat, double lng2, double lat2);
    }
}
