using System.Collections.Generic;
using System.Linq;
using FsCheck.Xunit;
using Xunit;

namespace FsCheck.Test
{
    public class PropertyAttributeTest {

        [Property]
        public void automatically_discover_Arbitrary_instances_from_parameter_types( 
            SomeType arg1, AnotherType arg2, SomeType[] arg3, List<AnotherType> arg4,
            int primitiveArg, string anotherPrimitiveArg ) 
        {
            // Trivial checks here.
            // The whole test is to see if FsCheck finds SomeType.MakeArb() and AnotherType.MakeArb()
            // and uses them to construct values for parameters.
            Assert.Equal( 42, arg1.X );
            Assert.Equal( "Bowties are cool", arg2.Y );
            Assert.True( arg3.All( a => a.X == 42 ) );
            Assert.True( arg4.All( a => a.Y == "Bowties are cool" ) );
        }

        public class SomeType
        {
            public int X;

            public static Arbitrary<SomeType> MakeArb() { return Gen.Constant( new SomeType { X = 42 } ).ToArbitrary(); }
        }

        public class AnotherType
        {
            public string Y;

            public static Arbitrary<AnotherType> MakeArb() { return Gen.Constant( new AnotherType { Y = "Bowties are cool" } ).ToArbitrary(); }
        }
    }
}