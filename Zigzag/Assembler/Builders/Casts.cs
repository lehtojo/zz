using System;

public static class Casts 
{
   public static Result Cast(Result result, Type from, Type to)
   {
      if (from == to)
      {
         return result;
      }

      if (from.IsTypeInherited(to)) // Determine whether the cast is a down cast
      {
         var base_offset = from.GetSupertypeBaseOffset(to) ?? throw new ApplicationException("Couldn't calculate base offset of a super type while building down cast");
         
         if (base_offset == 0)
         {
            return result;
         }
         
         var calculation = new CalculationHandle(result, 1, null, base_offset);

         return new Result(calculation, result.Format);
      }

      if (to.IsTypeInherited(from)) // Determine whether the cast is a up cast
      {
         var base_offset = to.GetSupertypeBaseOffset(from) ?? throw new ApplicationException("Couldn't calculate base offset of a super type while building up cast");
         
         if (base_offset == 0)
         {
            return result;
         }
         
         var calculation = new CalculationHandle(result, 1, null, -base_offset);
 
         return new Result(calculation, result.Format);
      }

      // This means that the cast is unsafe since the types have nothing in common
      return result;
   }

   public static Result Build(Unit unit, CastNode node)
   {
      var from = node.Object.GetType() ?? throw new ApplicationException("Object of a cast node was not resolved properly");
      var to = node.GetType() ?? throw new ApplicationException("Cast node was not resolved properly");

      var result = References.Get(unit, node.Object);
      result.Format = to.Format;

      return Cast(result, from, to);
   }
}