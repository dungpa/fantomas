﻿module internal Fantomas.CodePrinter

open System
open Fantomas.SourceParser
open Fantomas.FormatConfig

let rec genParsedInput = function
    | ImplFile im -> genImpFile im
    | SigFile si -> genSigFile si

and genImpFile = function
    | ParsedImplFileInput(hs, mns) ->
        // Each module is separated by a number of blank lines
        col sepNln mns genModuleOrNamespace

and genSigFile si = failwith "Not implemented yet"

and genModuleOrNamespace = function
    | ModuleOrNamespace(ats, px, ao, li, mds) -> col sepNln mds genModuleDecl

and genModuleDecl = function
    | Attributes(ats) -> col sepNln ats genAttribute
    | DoExpr(e) ->  genExpr e
    | Exception(ex) -> genException ex
    | HashDirective(s1, s2) -> 
        // print strings with quotes
        !- "#" -- s1 +> sepSpace -- sprintf "%A" s2 
    | Let(b) -> genBinding "let " b
    | LetRec(b::bs) -> genBinding "let rec " b +> sepNln +> col sepNln bs (genBinding "and ")
    | ModuleAbbrev(s1, s2) -> !- "module " -- s1 +> sepEq -- s2
    | NamespaceFragment(m) -> failwithf "NamespaceFragment is not supported yet: %O" m
    | NestedModule(ats, px, ao, s, mds) -> 
        colPost sepNln sepNln ats genAttribute 
        +> genPreXmlDoc px -- "module " +> opt sepSpace ao genAccess -- s +> sepEq
        +> indent +> sepNln +> col sepNln mds genModuleDecl +> unindent
    | Open(s) -> !- (sprintf "open %s" s)
    | Types(sts) -> col sepNln sts genTypeDefn
    | md -> failwithf "Unexpected pattern: %O" md

and genAccess(Access s) = !- s

and genAttribute(Attribute(li, e, isGetSet)) = 
    match e with
    // Special treatment for function application on attributes
    | ConstExpr(Const "()") -> !- (sprintf "[<%s>]" li)
    | e -> !- "[<" -- li +> genExpr e -- ">]"
    
and genPreXmlDoc(PreXmlDoc lines) = colPost sepNln sepNln lines (!-)

and genBinding prefix = function
    | LetBinding(ats, px, ao, isInline, isMutable, p, e, bk) ->
        // Figure out what to do with SynBindingKind
        colPost sepSpace sepNln ats genAttribute 
        +> genPreXmlDoc px -- prefix +> opt sepSpace ao genAccess
        +> ifElse isMutable (!- "mutable ") sepNone +> ifElse isInline (!- "inline ") id
        +> genPat p +> sepEq +> indent +> sepNln +> genExpr e +> unindent
    | MemberBinding(ats, px, ao, isInline, isInst, p, e, bk) ->
        // TODO: prefix doesn't make sense here
        colPost sepSpace sepNln ats genAttribute
        +> genPreXmlDoc px
        +> ifElse isInst (!- "static member ") (!- "member ")
        +> ifElse isInline (!- "inline ") id
        +> opt sepSpace ao genAccess
        +> genPat p +> sepEq +> genExpr e

and genExpr = function
    // Remove superfluous parens
    | Paren(Tuple es as e) -> genExpr e
    | Paren(ConstExpr(Const "()")) -> !- "()"
    | Paren e -> !- "(" +> genExpr e -- ")"
    | SingleExpr(kind, e) -> str kind +> sepSpace +> genExpr e
    | ConstExpr(Const s) -> !- s
    | NullExpr -> !- "null"
    // Not sure about the role of e1
    | Quote(e1, e2, isRaw) -> ifElse isRaw (!- "<@@ " +> genExpr e2 -- " @@>") (!- "<@ " +> genExpr e2 -- " @>")
    | TypedExpr(TypeTest, e, t) -> genExpr e -- " :? " +> genType t
    | TypedExpr(New, e, t) -> !- "new " +> genType t +> sepSpace +> genExpr e
    | TypedExpr(Downcast, e, t) -> genExpr e -- " :?> " +> genType t
    | TypedExpr(Upcast, e, t) -> genExpr e -- " :> " +> genType t
    | TypedExpr(Typed, e, t) -> genExpr e +> sepColon +> genType t
    | Tuple es -> !- "(" +> col sepComma es genExpr -- ")"
    // Figure out how to break long expressions into multiple lines
    | ArrayOrList(isArray, xs) -> 
        ifElse isArray (!- "[|" +> col sepComma xs genExpr -- "|]") (!- "[" +> col sepComma xs genExpr -- "]")
    | Record(xs) -> 
        !- "{ " 
        +> col sepSemi xs (fun ((LongIdentWithDots li, _), eo, _) -> opt sepNone eo (fun e -> !- li +> sepEq +> genExpr e)) 
        -- " }"
    | ObjExpr(t, x, bd, ims) ->
        !- "{ new " +> genType t -- " with" 
        +> incrIndent 2 +> indent +> sepNln +> col sepNln bd (genBinding "")
        +> colPre sepNln sepNln ims genInterfaceImpl -- " }" +> decrIndent 2 +> sepNln
    | While(e1, e2) -> 
        !- "while " +> genExpr e1 -- " do" 
        +> indent +> sepNln +> genExpr e2 +> unindent
    | For(s, e1, e2, e3, isUp) ->
        !- (sprintf "for %s = " s) +> genExpr e1 
        +> ifElse isUp (!- " to ") (!- " downto ")+> genExpr e2 -- " do" 
        +> indent +> sepNln +> genExpr e3 +> unindent
    // When does something has form of 'for i in e1 -> e2'?
    | ForEach(p, e1, e2) ->
        !- "for " +> genPat p -- " in " +> genExpr e1 -- " do" 
        +> indent +> sepNln +> genExpr e2 +> unindent
    // Not sure what it is different from ArrayOrListOfSeqExpr
    | CompExpr(isArray, e) ->
        ifElse isArray (!- "[|" +> genExpr e -- "|]") (!- "[" +> genExpr e -- "]")
    | ArrayOrListOfSeqExpr(isArray, e) -> ifElse isArray (!- "[|" +> genExpr e -- "|]") (!- "[" +> genExpr e -- "]")
    | Lambda(e, sp) -> !- "fun " +> genSimplePats sp +> sepArrow +> genExpr e
    | Match(e, cs) -> 
        !- "match " +> genExpr e -- " with" 
        +> indent +> sepNln +> col sepNln cs genMatchClause +> unindent +> sepNln
    | App(e1, e2) -> genExpr e1 +> sepSpace +> genExpr e2
    | TypeApp(e, ts) -> genExpr e -- "<" +> col sepComma ts genType -- ">"
    // Not really understand it
    | LetOrUse(isRec, isUse, bs, e) ->
        ifElse isUse (!- "use ") (ifElse isRec (!- "let rec ") (!- "let "))
        +> col sepSpace bs (genBinding "") +> sepEq +> genExpr e
    | TryWith(e, cs) ->  !- "try " +> genExpr e ++ "with" +> sepNln +> col sepNln cs genMatchClause
    | TryFinally(e1, e2) -> !- "try " +> genExpr e1 ++ "finally" +> indent +> sepNln +> genExpr e2 +> unindent
    // May process the boolean flag later
    | Sequential(e1, e2) -> genExpr e1 +> sepNln +> genExpr e2
    | IfThenElse(e1, e2, Some e3) -> !- "if " +> genExpr e1 -- " then " +> genExpr e2 -- " else " +> genExpr e3
    | IfThenElse(e1, e2, None) -> !- "if " +> genExpr e1 -- " then " +> genExpr e2
    | Var(li) -> !- li
    | LongIdentSet(s, e) -> !- (sprintf "%s <- " s) +> genExpr e
    | DotIndexedGet(e, es) -> genExpr e -- ".[" +> col sepComma es genExpr -- "]"
    | DotIndexedSet(e1, es, e2) -> genExpr e1 -- ".[" +> col sepComma es genExpr -- "] <- " +> genExpr e2
    | DotGet(e, s) -> genExpr e -- sprintf ".%s" s
    | DotSet(e1, s, e2) -> genExpr e1 -- sprintf ".%s <- " s +> genExpr e2
    | TraitCall(ss, msg, e) -> 
        let types = 
            match ss with
            | [] -> invalidArg "ss" "List of type names should not be empty"
            | [s] -> !- s
            | ss -> !- "(" +> col (!- " or ") ss (!-) -- ")"
        !- "(" +> types +> sepColon +> genMemberSig msg +> sepSpace +> genExpr e -- ")"
    | LetOrUseBang(isUse, p, e1, e2) ->
        ifElse isUse (!- "use! ") (!- "let! ") 
        +> genPat p -- " = " +> genExpr e1 -- " in " +> genExpr e2
    | e -> failwithf "Unexpected pattern: %O" e

and genTypeDefn(TypeDef(ats, px, ao, tds, tcs, tdr, ms, li)) = 
    let typeName = 
        colPost sepSpace sepNln ats genAttribute 
        +> genPreXmlDoc px -- "type " +> opt sepSpace ao genAccess -- li
    match tdr with
    | Simple(TDSREnum ecs) ->
        typeName +> sepEq 
        +> indent +> sepNln
        +> col sepNln ecs (genEnumCase true)
        // Add newline after unindent to be spacing-correct
        +> unindent +> sepNln 
    | Simple(TDSRUnion(ao', xs)) ->
        typeName +> sepEq 
        +> indent +> sepNln +> opt sepNln ao' genAccess 
        +> col sepNln xs (genUnionCase true)
        +> unindent +> sepNln
    | Simple(TDSRRecord(ao', fs)) ->
        typeName +> sepEq 
        +> indent +> sepNln +> opt sepNln ao' genAccess -- "{ " +> col sepSemiNln fs genField -- " }" 
        +> unindent +> sepNln 
    | Simple TDSRNone -> 
        typeName +> sepNln
    | Simple(TDSRTypeAbbrev t) -> 
        typeName +> sepEq +> genType t +> sepNln
    | Simple TDSRGeneral -> id
    | ObjectModel(tdk, md) -> failwith "Not implemented yet"

and genException(ExceptionDef(ats, px, ao, uc, ms)) = 
    colPost sepSpace sepNln ats genAttribute 
    +> genPreXmlDoc px -- "exception " +> opt sepSpace ao genAccess +> genUnionCase false uc
    +> sepNln +> colPost sepNln sepNln ms genMemberDefn

and genUnionCase hasBar (UnionCase(ats, px, ao, s, UnionCaseType fs)) = 
    // Access option doesn't seem relevant here
    genPreXmlDoc px
    +> ifElse hasBar (!- "| ") sepNone -- s 
    +> colPre sepStar wordOf fs genField

and genEnumCase hasBar (EnumCase(ats, px, s, Const c)) =
    genPreXmlDoc px
    +> ifElse hasBar (!- "| ") sepNone 
    +> colPost sepSpace sepSpace ats genAttribute -- s +> sepEq -- c    

and genField(Field(ats, px, ao, isStatic, t, so)) = 
    opt sepColon so (!-) +> genType t

and genType = function
    | THashConstraint t -> !- "#" +> genType t
    | TMeasurePower(t, n) -> genType t -- "^" +> str n
    | TMeasureDivide(t1, t2) -> genType t1 -- " / " +> genType t2
    | TStaticConstant(Const c) -> !- c
    | TStaticConstantExpr(e) -> genExpr e
    // Not sure about this case
    | TStaticConstantNamed(t1, t2) -> genType t1 -- "=" +> genType t2
    | TArray(t, n) -> genType t +> rep n (!- "[]")
    | TAnon -> sepWild
    | TVar tp -> !- "'" +> genTypePar tp 
    | TFun(t1, t2) -> genType t1 +> sepArrow +> genType t2
    | TApp(t, ts, isPostfix) -> 
        let postForm = 
            match ts with
            | [] -> invalidArg "ts" "List of types should not be empty"
            | [t'] -> genType t' +> sepSpace +> genType t
            | ts -> !- "(" +> col sepComma ts genType -- ") " +> genType t
        ifElse isPostfix postForm (genType t -- "<" +> col sepComma ts genType -- ">")
    | TLongIdentApp(t, li, ts) -> genType t -- li -- "<" +> col sepComma ts genType -- ">"
    // Not sure why the bool value could change '*' to '/'
    | TTuple ts -> col sepStar ts (snd >> genType)
    // Revise this case later
    | TWithGlobalConstraints(t, tcs) -> genType t -- " with " +> col wordAnd tcs genTypeConstr
    | TLongIdent li -> !- li
    | t -> failwithf "Unexpected pattern: %O" t

and genTypePar tp = id

and genTypeConstr tc = id

and genInterfaceImpl ii = id

and genMatchClause mc = id

and genMemberSig ms = id

and genMemberDefn = function
    | MDNestedType(td, ao) -> id
    | MDOpen(so) -> id
    | MDImplicitInherit(t, e, so) -> id
    | MDInherit(t, so) -> id
    | MDValField(ats, px, ao, t, so) -> id
    | MDImplicitCtor(ats, ao, ps, so) -> id
    | MDMember(bo) -> id
    | MDLetBindings(isStatic, isRec, bs) -> id
    | MDInterface(t, mdo) -> id
    | MDAutoProperty(ats, px, ao, mk, e, s) -> id
    | md -> failwithf "Unexpected pattern: %O" md

and genSimplePats sp = id

and genPat = function
    | PatOptionalVal(s) -> !- (sprintf "?%s" s)
    // Not sure what it is about
    | PatAttrib(p, attrs) -> id
    | PatOr(p1, p2) -> genPat p1 -- " | " +> genPat p2
    | PatAnds(ps) -> col (!- " & ") ps genPat
    | PatNullary PatNull -> !- "null"
    | PatNullary PatWild -> id
    | PatTyped(p, t) -> genPat p +> sepColon +> genType t
    | PatNamed(ao, p, s) ->  opt sepSpace ao genAccess -- s +> genPat p
    | PatLongIdent(ao, li, ps) -> opt sepSpace ao genAccess -- li +> sepSpace +> col sepSpace ps genPat
    | PatParen(PatConst(Const s)) -> !- s
    | PatParen(p) -> !- "(" +> genPat p -- ")"
    | PatSeq(PatTuple, ps) -> col sepComma ps genPat
    | PatSeq(PatList, ps) -> !- "[" +> col sepSemi ps genPat -- "]"
    | PatSeq(PatArray, ps) -> !- "[|" +> col sepSemi ps genPat -- "|]"
    | PatRecord(xs) -> 
        !- "{ " +> col sepSemi xs (fun ((LongIdent li, Ident s), p) -> !- (sprintf "%s = %s" li s) +> genPat p) -- " }"
    | PatConst(Const s) -> !- s
    | PatIsInst(t) -> !- " :? " +> genType t
    | PatQuoteExpr e -> !- "<@ " +> genExpr e -- " @>"
    | p -> failwithf "Unexpected pattern: %O" p
