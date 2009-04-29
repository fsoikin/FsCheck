﻿#light

namespace FsCheck

//module public Fluent //don't add a module - otherwise every class needs to be qualified

open System
open System.Linq
open System.ComponentModel
open System.Collections.Generic
open Common

//TODO:
//Within -> rely on testing frameworks
//Throws-> probqbly just don't do it - rely on unit testing frameworks
//config
//checkAll?
//

type WeightAndValue<'a>(weight:int,value:'a) =
    member x.Weight = weight
    member x.Value = value
    
type Any = 
    static member OfType<'a>() = arbitrary<'a>
    static member Value (value) = constant value
    static member ValueIn (values : seq<_>) = values |> Seq.to_list |> elements
    static member IntBetween (l,h) = choose (l,h)
    static member GeneratorIn (generators : seq<Gen<_>>) = generators |> Seq.to_list |> oneof
    static member WeighedGeneratorIn ( weighedValues : seq<WeightAndValue<Gen<'a>>> ) =
        weighedValues |> Seq.map (fun wv -> (wv.Weight, wv.Value)) |> Seq.to_list |> frequency
    static member SequenceOf<'a> (generators:seq<Gen<_>>) = generators |> Seq.to_list |> sequence |> fmapGen (fun list -> new List<'a>(list))

type Shrink =
    static member Type<'a>() = shrink<'a>

[<AbstractClass>]
type UnbrowsableObject() =
    inherit obj()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    override x.Equals(other) = base.Equals(other)
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    override x.GetHashCode() = base.GetHashCode()
//    [<EditorBrowsable(EditorBrowsableState.Never)>]
//    override x.GetType() = base.GetType() //GetType cannot be overridden
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    override x.ToString() = base.ToString()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    abstract Build : unit -> Property
    member x.QuickCheck() = quickCheck <| x.Build()
    member x.VerboseCheck() = verboseCheck <| x.Build()

and SpecBuilder<'a>( generator0:'a Gen
                   , shrinker0: 'a -> 'a seq
                   , assertion0:'a -> Property
                   , conditions:('a -> bool) list
                   , collects:('a -> string) list
                   , classifies:(('a -> bool) * string) list) =
    inherit UnbrowsableObject()
    override x.Build() =
            let conditions' a = conditions |> List.fold_left (fun s f -> s && f a) true
            let collects' a prop = collects |> List.fold_left (fun prop f -> prop |> collect (f a)) prop
            let classifies' a prop = classifies |> List.fold_left (fun prop (f,name) -> prop |> classify (f a) name) prop  
            forAllShrink generator0 shrinker0 (fun a -> (conditions' a) ==> lazy (assertion0 a) |> collects' a |> classifies' a)
    member x.When( condition:Func<'a,bool> ) = 
        SpecBuilder<'a>(generator0, shrinker0, assertion0, (fun a -> condition.Invoke(a))::conditions, collects, classifies)
    member x.Collect(collectedValue:Func<'a,string>)=
        SpecBuilder<'a>(generator0, shrinker0,assertion0,conditions,(fun a -> collectedValue.Invoke(a))::collects,classifies)
    member x.Classify(filter:Func<'a,bool>,name:string) =
        SpecBuilder<'a>(generator0, shrinker0,assertion0,conditions,collects,((fun a -> filter.Invoke(a)),name)::classifies)
    member x.Shrink(shrinker:Func<'a,'a seq>) =
        SpecBuilder<'a>( generator0, shrinker.Invoke, assertion0, conditions, collects, classifies)
    member x.Label( name:string ) =
        SpecBuilder<'a>(generator0, shrinker0, label name << assertion0,conditions, collects, classifies)
    [<OverloadIDAttribute("0")>]
    member x.And(assertion : Func<'a,bool>) =
        SpecBuilder<'a>( generator0, shrinker0, (fun a -> (assertion0 a) .&. (assertion.Invoke(a))), conditions, collects, classifies)
    [<OverloadIDAttribute("1")>]
    member x.And(assertion : Func<'a,bool>, name:string ) =
        SpecBuilder<'a>( generator0, shrinker0, (fun a -> (assertion0 a) .&. (label name (assertion.Invoke(a)))), conditions, collects, classifies)
    [<OverloadIDAttribute("0")>]
    member x.Or(assertion : Func<'a,bool>) =
        SpecBuilder<'a>( generator0, shrinker0, (fun a -> (assertion0 a) .|. (assertion.Invoke(a))), conditions, collects, classifies)
    [<OverloadIDAttribute("1")>]
    member x.Or(assertion : Func<'a,bool>, name:string ) =
        SpecBuilder<'a>( generator0, shrinker0, (fun a -> (assertion0 a) .|. (label name (assertion.Invoke(a)))), conditions, collects, classifies)
    member x.AndFor<'b>(generator:'b Gen, assertion:Func<'b,bool>) =
        SpecBuilder<'a,'b>  (generator0
                            ,shrinker0 
                            ,generator
                            ,shrink
                            ,fun a b -> (assertion0 a) .&. property (assertion.Invoke(b))
                            ,conditions |> List.map (fun f -> (fun a b -> f a))
                            ,collects |> List.map (fun f -> (fun a b -> f a))
                            ,classifies |> List.map (fun (f,name) -> ((fun a b -> f a),name))
                            )
  
       
and SpecBuilder<'a,'b>( generator0:'a Gen
                      , shrinker0: 'a -> 'a seq
                      , generator1:'b Gen
                      , shrinker1: 'b -> 'b seq
                      , assertion0:'a -> 'b -> Property
                      , conditions:('a -> 'b -> bool) list
                      , collects:('a -> 'b -> string) list
                      , classifies:(('a -> 'b -> bool) * string) list) = 
    inherit UnbrowsableObject()
    override x.Build() =
            let conditions' a b = conditions |> List.fold_left (fun s f -> s && f a b) true
            let collects' a b prop = collects |> List.fold_left (fun prop f -> prop |> collect (f a b)) prop
            let classifies' a b prop = classifies |> List.fold_left (fun prop (f,name) -> prop |> classify (f a b) name) prop  
            forAll generator0 (fun a -> forAll generator1 (fun b -> (conditions' a b) ==> lazy (assertion0 a b) |> collects' a b |> classifies' a b))
    member x.When( condition:Func<'a,'b,bool> ) = 
        SpecBuilder<'a,'b>(generator0, shrinker0, generator1, shrinker1, assertion0, (fun a b -> condition.Invoke(a,b))::conditions, collects, classifies)
    member x.Collect(collectedValue:Func<'a,'b,string>)=
        SpecBuilder<'a,'b>(generator0, shrinker0, generator1, shrinker1, assertion0,conditions,(fun a b -> collectedValue.Invoke(a,b))::collects,classifies)
    member x.Classify(filter:Func<'a,'b,bool>,name:string) =
        SpecBuilder<'a,'b>(generator0, shrinker0,generator1, shrinker1,assertion0,conditions,collects,((fun a b -> filter.Invoke(a,b)),name)::classifies)
    member x.Shrink(shrinker:Func<'b,'b seq>) =
        SpecBuilder<'a,'b>( generator0, shrinker0, generator1, shrinker.Invoke, assertion0, conditions, collects, classifies)
    member x.Label( name:string ) =
        SpecBuilder<'a,'b>(generator0, shrinker0, generator1, shrinker1, (fun a b-> label name (assertion0 a b)),conditions, collects, classifies)
    [<OverloadIDAttribute("0")>]
    member x.And(assertion : Func<'a,'b,bool>) =
        SpecBuilder<'a,'b>( generator0, shrinker0, generator1, shrinker1,
            (fun a b -> (assertion0 a b) .&. (assertion.Invoke(a, b))) , conditions, collects, classifies)
    [<OverloadIDAttribute("1")>]
    member x.And(assertion : Func<'a,'b,bool>, name:string ) =
        SpecBuilder<'a,'b>( generator0, shrinker0, generator1, shrinker1, 
            (fun a b -> (assertion0 a b) .&. (label name (assertion.Invoke(a,b)))), conditions, collects, classifies)
    [<OverloadIDAttribute("0")>]
    member x.Or(assertion : Func<'a,'b,bool>) =
        SpecBuilder<'a,'b>( generator0, shrinker0, generator1, shrinker1, 
            (fun a b -> (assertion0 a b) .|. (assertion.Invoke(a,b))), conditions, collects, classifies)
    [<OverloadIDAttribute("1")>]
    member x.Or(assertion : Func<'a,'b,bool>, name:string ) =
        SpecBuilder<'a,'b>( generator0, shrinker0, generator1, shrinker1, 
            (fun a b-> (assertion0 a b) .|. (label name (assertion.Invoke(a,b)))), conditions, collects, classifies)
//    member x.AndFor<'c>(generator:'c Gen, assertion:Func<'c,bool>) =
//        SpecBuilder<'a,'b,'c>   (generator0
//                                ,generator1
//                                ,generator
//                                ,fun a b c -> (assertion0 a b) .&. property (assertion.Invoke(c))
//                                ,conditions |> List.map (fun f -> Func<'a,'b,'c,bool>(fun a b c -> f.Invoke(a,b)))
//                                ,collects |> List.map (fun f -> Func<'a,'b,'c,string>(fun a b c -> f.Invoke(a,b)))
//                                ,classifies |> List.map (fun (f,name) -> (Func<'a,'b,'c,bool>(fun a b c -> f.Invoke(a,b)),name))
//                                )
//and SpecBuilder<'a,'b,'c>(generator0:'a Gen,generator1:'b Gen, generator2:'c Gen, 
//                            assertion0:'a -> 'b -> 'c -> Property,
//                            conditions:Func<'a,'b,'c,bool> list, 
//                            collects:Func<'a,'b,'c,string> list, 
//                            classifies:(Func<'a,'b,'c,bool> * string) list) = 
//    inherit UnbrowsableObject()
//    override x.Build() =
//            let conditions' a b c = conditions |> List.fold_left (fun s f -> s && f.Invoke(a,b,c)) true
//            let collects' a b c prop = collects |> List.fold_left (fun prop f -> prop |> collect (f.Invoke(a,b,c))) prop
//            let classifies' a b c prop = classifies |> List.fold_left (fun prop (f,name) -> prop |> classify (f.Invoke(a,b,c)) name) prop  
//            forAll generator0 (fun a -> 
//            forAll generator1 (fun b -> 
//            forAll generator2 (fun c ->
//                (conditions' a b c) ==> lazy (assertion0 a b c) |> collects' a b c |> classifies' a b c))) 
//    member x.When( condition:Func<'a,'b,'c,bool> ) = 
//        SpecBuilder<'a,'b,'c>(generator0, generator1, generator2, assertion0, condition::conditions, collects, classifies)
//    member x.Collect(collectedValue:Func<'a,'b,'c,string>)=
//        SpecBuilder<'a,'b,'c>(generator0, generator1, generator2, assertion0,conditions,collectedValue::collects,classifies)
//    member x.Classify(filter:Func<'a,'b,'c,bool>,name:string) =
//        SpecBuilder<'a,'b,'c>(generator0,generator1,generator2, assertion0,conditions,collects,(filter,name)::classifies)         
                
type Spec =
    [<OverloadIDAttribute("ForAnyFunc")>]
    static member ForAny(assertion:Func<'a,bool>) =
        Spec.For(Any.OfType<'a>(),assertion)
    [<OverloadIDAttribute("ForAnyAction")>]
    static member ForAny(assertion:Action<'a>) =
        Spec.For(Any.OfType<'a>(),assertion)
    [<OverloadIDAttribute("ForAnyFunc2")>]
    static member ForAny(assertion:Func<'a,'b,bool>) =
        Spec.For(Any.OfType<'a>(),Any.OfType<'b>(),assertion)
    [<OverloadIDAttribute("ForAnyAction2")>]
    static member ForAny(assertion:Action<'a,'b>) =
        Spec.For(Any.OfType<'a>(),Any.OfType<'b>(),assertion)
        
    [<OverloadIDAttribute("For1Func")>]
    static member For(generator:'a Gen, assertion:Func<'a,bool>) =
        SpecBuilder<'a>(generator, shrink, property << assertion.Invoke, [], [], [])
    [<OverloadIDAttribute("For1Action")>]
    static member For(generator:'a Gen, assertion:Action<'a>) =
        SpecBuilder<'a>(generator, shrink, property << assertion.Invoke, [], [], [])
    [<OverloadIDAttribute("For2Func")>]
    static member For(generator1:'a Gen,generator2:'b Gen, assertion:Func<'a,'b,bool>) =
        SpecBuilder<'a,'b>(generator1, shrink, generator2, shrink, (fun a b -> property <| assertion.Invoke(a,b)),[],[],[])
    [<OverloadIDAttribute("For2Action")>]
    static member For(generator1:'a Gen,generator2:'b Gen, assertion:Action<'a,'b>) =
        SpecBuilder<'a,'b>(generator1, shrink, generator2, shrink, (fun a b -> property <| assertion.Invoke(a,b)),[],[],[])

open Generator

[<System.Runtime.CompilerServices.Extension>]
type GeneratorExtensions = 
    [<System.Runtime.CompilerServices.Extension>]
    static member Select(g:Gen<_>, selector : Func<_,_>) = g.Map(fun a -> selector.Invoke(a))
    
    [<System.Runtime.CompilerServices.Extension>]
    static member Where(g:Gen<_>, predicate : Func<_,_>) = suchThat (fun a -> predicate.Invoke(a)) g
    
    [<System.Runtime.CompilerServices.Extension>]
    static member SelectMany(source:Gen<_>, f:Func<_, Gen<_>>) = 
        gen { let! a = source
              return! f.Invoke(a) }
    
    [<System.Runtime.CompilerServices.Extension>]
    static member SelectMany(source:Gen<_>, f:Func<_, Gen<_>>, select:Func<_,_,_>) =
        gen { let! a = source
              let! b = f.Invoke(a)
              return select.Invoke(a,b) }
    
    [<System.Runtime.CompilerServices.Extension>]
    static member MakeList<'a> (generator) = listOf generator |> fmapGen (fun list -> new List<'a>(list))
    
    [<System.Runtime.CompilerServices.Extension>]
    static member MakeNonEmptyList<'a> (generator) = nonEmptyListOf generator |> fmapGen (fun list -> new List<'a>(list))
    
    [<System.Runtime.CompilerServices.Extension>]
    static member MakeListOfLength<'a> (generator, count) = vectorOf count generator |> fmapGen (fun list -> new List<'a>(list))
    
init.Force()