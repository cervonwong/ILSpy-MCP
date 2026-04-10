// Phase 10 pagination fixture sibling file - Implementors scenario.
// IImplementorsTarget has 111 implementing classes (70 direct + 1 anchor + 40 transitive)
// so find_implementors pagination + sort can be exercised at the 100/111 boundary.
//
// Do not add/remove classes without updating the pagination assertions in
// Tests/Tools/FindImplementorsToolTests.cs Pagination_* facts.

namespace ILSpy.Mcp.TestTargets.Pagination.Implementors;

// Target interface with 111 implementing classes.
// Mix of direct (implementing IImplementorsTarget directly) and transitive
// (extending a direct implementor) so the direct/transitive sort can be exercised.
public interface IImplementorsTarget { }

// 70 direct implementors
public class ImplDirect001 : IImplementorsTarget { }
public class ImplDirect002 : IImplementorsTarget { }
public class ImplDirect003 : IImplementorsTarget { }
public class ImplDirect004 : IImplementorsTarget { }
public class ImplDirect005 : IImplementorsTarget { }
public class ImplDirect006 : IImplementorsTarget { }
public class ImplDirect007 : IImplementorsTarget { }
public class ImplDirect008 : IImplementorsTarget { }
public class ImplDirect009 : IImplementorsTarget { }
public class ImplDirect010 : IImplementorsTarget { }
public class ImplDirect011 : IImplementorsTarget { }
public class ImplDirect012 : IImplementorsTarget { }
public class ImplDirect013 : IImplementorsTarget { }
public class ImplDirect014 : IImplementorsTarget { }
public class ImplDirect015 : IImplementorsTarget { }
public class ImplDirect016 : IImplementorsTarget { }
public class ImplDirect017 : IImplementorsTarget { }
public class ImplDirect018 : IImplementorsTarget { }
public class ImplDirect019 : IImplementorsTarget { }
public class ImplDirect020 : IImplementorsTarget { }
public class ImplDirect021 : IImplementorsTarget { }
public class ImplDirect022 : IImplementorsTarget { }
public class ImplDirect023 : IImplementorsTarget { }
public class ImplDirect024 : IImplementorsTarget { }
public class ImplDirect025 : IImplementorsTarget { }
public class ImplDirect026 : IImplementorsTarget { }
public class ImplDirect027 : IImplementorsTarget { }
public class ImplDirect028 : IImplementorsTarget { }
public class ImplDirect029 : IImplementorsTarget { }
public class ImplDirect030 : IImplementorsTarget { }
public class ImplDirect031 : IImplementorsTarget { }
public class ImplDirect032 : IImplementorsTarget { }
public class ImplDirect033 : IImplementorsTarget { }
public class ImplDirect034 : IImplementorsTarget { }
public class ImplDirect035 : IImplementorsTarget { }
public class ImplDirect036 : IImplementorsTarget { }
public class ImplDirect037 : IImplementorsTarget { }
public class ImplDirect038 : IImplementorsTarget { }
public class ImplDirect039 : IImplementorsTarget { }
public class ImplDirect040 : IImplementorsTarget { }
public class ImplDirect041 : IImplementorsTarget { }
public class ImplDirect042 : IImplementorsTarget { }
public class ImplDirect043 : IImplementorsTarget { }
public class ImplDirect044 : IImplementorsTarget { }
public class ImplDirect045 : IImplementorsTarget { }
public class ImplDirect046 : IImplementorsTarget { }
public class ImplDirect047 : IImplementorsTarget { }
public class ImplDirect048 : IImplementorsTarget { }
public class ImplDirect049 : IImplementorsTarget { }
public class ImplDirect050 : IImplementorsTarget { }
public class ImplDirect051 : IImplementorsTarget { }
public class ImplDirect052 : IImplementorsTarget { }
public class ImplDirect053 : IImplementorsTarget { }
public class ImplDirect054 : IImplementorsTarget { }
public class ImplDirect055 : IImplementorsTarget { }
public class ImplDirect056 : IImplementorsTarget { }
public class ImplDirect057 : IImplementorsTarget { }
public class ImplDirect058 : IImplementorsTarget { }
public class ImplDirect059 : IImplementorsTarget { }
public class ImplDirect060 : IImplementorsTarget { }
public class ImplDirect061 : IImplementorsTarget { }
public class ImplDirect062 : IImplementorsTarget { }
public class ImplDirect063 : IImplementorsTarget { }
public class ImplDirect064 : IImplementorsTarget { }
public class ImplDirect065 : IImplementorsTarget { }
public class ImplDirect066 : IImplementorsTarget { }
public class ImplDirect067 : IImplementorsTarget { }
public class ImplDirect068 : IImplementorsTarget { }
public class ImplDirect069 : IImplementorsTarget { }
public class ImplDirect070 : IImplementorsTarget { }

// One anchor class to serve as base for transitive implementors
public class ImplDirectAnchor : IImplementorsTarget { }

// 40 transitive implementors (extending ImplDirectAnchor)
public class ImplTransitive001 : ImplDirectAnchor { }
public class ImplTransitive002 : ImplDirectAnchor { }
public class ImplTransitive003 : ImplDirectAnchor { }
public class ImplTransitive004 : ImplDirectAnchor { }
public class ImplTransitive005 : ImplDirectAnchor { }
public class ImplTransitive006 : ImplDirectAnchor { }
public class ImplTransitive007 : ImplDirectAnchor { }
public class ImplTransitive008 : ImplDirectAnchor { }
public class ImplTransitive009 : ImplDirectAnchor { }
public class ImplTransitive010 : ImplDirectAnchor { }
public class ImplTransitive011 : ImplDirectAnchor { }
public class ImplTransitive012 : ImplDirectAnchor { }
public class ImplTransitive013 : ImplDirectAnchor { }
public class ImplTransitive014 : ImplDirectAnchor { }
public class ImplTransitive015 : ImplDirectAnchor { }
public class ImplTransitive016 : ImplDirectAnchor { }
public class ImplTransitive017 : ImplDirectAnchor { }
public class ImplTransitive018 : ImplDirectAnchor { }
public class ImplTransitive019 : ImplDirectAnchor { }
public class ImplTransitive020 : ImplDirectAnchor { }
public class ImplTransitive021 : ImplDirectAnchor { }
public class ImplTransitive022 : ImplDirectAnchor { }
public class ImplTransitive023 : ImplDirectAnchor { }
public class ImplTransitive024 : ImplDirectAnchor { }
public class ImplTransitive025 : ImplDirectAnchor { }
public class ImplTransitive026 : ImplDirectAnchor { }
public class ImplTransitive027 : ImplDirectAnchor { }
public class ImplTransitive028 : ImplDirectAnchor { }
public class ImplTransitive029 : ImplDirectAnchor { }
public class ImplTransitive030 : ImplDirectAnchor { }
public class ImplTransitive031 : ImplDirectAnchor { }
public class ImplTransitive032 : ImplDirectAnchor { }
public class ImplTransitive033 : ImplDirectAnchor { }
public class ImplTransitive034 : ImplDirectAnchor { }
public class ImplTransitive035 : ImplDirectAnchor { }
public class ImplTransitive036 : ImplDirectAnchor { }
public class ImplTransitive037 : ImplDirectAnchor { }
public class ImplTransitive038 : ImplDirectAnchor { }
public class ImplTransitive039 : ImplDirectAnchor { }
public class ImplTransitive040 : ImplDirectAnchor { }
