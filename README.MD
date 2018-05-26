## NGeoKDBush 

This library allows indexing geographical points, and then querying for all points  
of interest that are some distance from some location.  

The points will be returned ordered by being closet to furthest.


It implements fast [nearest neighbors](https://en.wikipedia.org/wiki/Nearest_neighbor_search) queries
for locations on Earth, taking Earth curvature and date line wrapping into account.
Direct port of [geokdbush](https://github.com/mourner/geokdbush)
Which itself was inspired by [sphere-knn](https://github.com/darkskyapp/sphere-knn), but uses a different algorithm.

A geographic extension for [nkdbush](https://github.com/Shrulik/NKDbush).

### Example

```csharp

var index = new KDBush<City>(cities.ToArray(), p => p.Lon, p => p.Lat);
var geoKdBush = new GeoKDBush<City>();
var nearest = geoKdBush.Around(index, -119.7051, 34.4363, 5);

```

### API

#### Around(IKDBush<T> index, double lng, double lat, int? maxResults = null, double? maxDistance = null, Predicate<T> predicate = null)

Returns an array of the closest points from a given location in order of increasing distance.

- `index`: [nkdbush](https://github.com/Shrulik/NKDbush) index.
- `lng`: query point longitude.
- `lat`: query point latitude.
- `maxResults`: (optional) maximum number of points to return (`int.MaxValue` by default).
- `maxDistance`: (optional) maximum distance in kilometers to search within (`double.MaxValue` by default).
- `predicate`: (optional) a lambda to filter the results with.

#### Distance(double lng, double lat, double lng2, double lat2)

Returns great circle distance between two locations in kilometers.

### Performance

I have found the performance of this port fast enough for my purposes,  
so I haven't yet had the justification to run micro-bechmarks.  
However, I'm sure many performance improvements are possilbe and I might get to it someday.  
