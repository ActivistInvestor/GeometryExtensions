/// CurveContainmentExtensions.cs
/// ActivistInvestor / Tony Tanzillo

/// Prerequisites: Requires a reference to acdbmgdbrep.dll

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.BoundaryRepresentation; 
using AcBr = Autodesk.AutoCAD.BoundaryRepresentation;

namespace Autodesk.AutoCAD.Geometry.Extensions
{
   public static class CurveContainmentExtensions
   {

      /// <summary>
      /// IEnumerable<Point3d>.ContainedBy() extension.
      /// 
      /// Reduces a sequence of Point3d to the subset that
      /// are contained within a given closed, planar Curve.
      /// 
      /// Points are ortho-projected onto the plane of the
      /// curve for testing, and can optionally be returned
      /// in lieu of the input points by specifying true in
      /// the <paramref name="projected"/> argument.
      /// </summary>
      /// <remarks>
      /// Older versions of this code used optimized trivial 
      /// rejection to eliminate points that lie outside the 
      /// geometric extents of the curve, before submitting 
      /// them to more-expensive testing using a BRep entity.
      /// 
      /// However, it was deduced through testing that the
      /// BRep class's GetPointContainment() method already
      /// uses similar trivial rejection tactics internally,
      /// making caller-performed trivial rejection redundant
      /// and self-defeating.
      /// 
      /// Example:
      /// <code>
      /// 
      ///   Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
      ///  
      ///   IEnumerable<Point3d> points =   // assign to a sequence of Point3d
      ///    
      ///   Curve curve =    // Assign to a closed, planar Curve entity.
      ///    
      ///   // Get the subset of points that lie inside the curve:
      ///    
      ///   IEnumerable<Point3d> contained = points.ContainedBy(curve);
      ///    
      ///   ed.WriteMessage($"\nFound {contained.Count()} points inside the curve.");
      ///    
      /// </code>
      /// </remarks>
      /// <param name="input">The input sequence of Point3d objects</param>
      /// <param name="curve">The curve containing the resulting points</param>
      /// <param name="includeOnBoundary">A value indicating if points
      /// that lie exactly on the Curve's boundary are included</param>
      /// <param name="projected">A value indicating if the resulting
      /// points are projected onto the plane of the Curve</param>
      /// <returns>A sequence of Point3d containing the subset of
      /// points from the input sequence that lie inside or on the
      /// boundary of the given Curve.</returns>
      /// <exception cref="ArgumentException"></exception>
      /// <exception cref="InvalidOperationException"></exception>

      public static IEnumerable<Point3d> ContainedBy(this IEnumerable<Point3d> input, Curve curve, bool includeOnBoundary = false, bool projected = false)
      {
         if(input == null) throw new ArgumentNullException(nameof(input)); 
         if(curve == null) throw new ArgumentNullException(nameof(curve));
         if(!(curve.Closed && curve.IsPlanar))
            throw new ArgumentException("requires a closed, planar curve");
         using(Region region = RegionFromClosedCurve(curve))
         {
            if(region == null)
               throw new InvalidOperationException("Region creation from curve failed.");
            using(Brep brep = new Brep(region))
            {
               if(brep == null)
                  throw new InvalidOperationException("failed to get boundry representation");
               Plane plane = region.GetPlane();
               foreach(Point3d point in input)
               {
                  Point3d pointOnPlane = point.OrthoProject(plane));
                  if(brep.Contains(pointOnPlane, includeOnBoundary))
                     yield return projected ? pointOnPlane : point;
               }
            }
         }
      }

      /// <summary>
      /// Returns a value indicating if the given BRep entity
      /// contains a given point on the BRep entity's plane.
      /// </summary>
      /// <param name="brep">The BRep entity to test against the point</param>
      /// <param name="pointOnPlane">The Point3d on the plane of the BRep entity</param>
      /// <param name="onEdge">A value indicating if points that lie
      /// exactly on the boundary of the BRep are inclusive.</param>
      /// <returns>A value indicating if the Point is on or within the BRep entity</returns>

      public static bool Contains(this Brep brep, Point3d pointOnPlane, bool onEdge)
      {
         PointContainment result = PointContainment.Outside;
         using(BrepEntity ent = brep.GetPointContainment(pointOnPlane, out result))
         {
            return result == PointContainment.Inside || ent is AcBr.Face
               || onEdge && ent is AcBr.Edge;
         }
      }

      public static PointContainment GetContainment(this Brep brep, Point3d pointOnPlane)
      {
         PointContainment result = PointContainment.Outside;
         using(BrepEntity ent = brep.GetPointContainment(pointOnPlane, out result))
         {
            if(ent is AcBr.Face)
               return PointContainment.Inside;
            else if(ent is AcBr.Edge)
               return PointContainment.OnBoundary;
            else
               return PointContainment.Outside;
         }
      }

      /// <summary>
      /// Attempts to create a region from a closed curve.
      /// </summary>
      /// <param name="curve"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentException"></exception>
      /// <exception cref="InvalidOperationException"></exception>

      [return: CallerMustDispose]
      public static Region RegionFromClosedCurve(Curve curve)
      {
         if(!(curve.Closed && curve.IsPlanar))
            throw new ArgumentException("requires a closed, planar curve");
         using(DBObjectCollection curves = new DBObjectCollection())
         {
            curves.Add(curve);
            using(DBObjectCollection regions = Region.CreateFromCurves(curves))
            {
               if(regions == null || regions.Count == 0)
                  throw new InvalidOperationException("Failed to create regions");
               if(regions.Count > 1)
                  throw new InvalidOperationException("Multiple regions created");
               return regions.Cast<Region>().First();
            }
         }
      }

      [return: CallerMustDispose]
      public static DBObjectCollection RegionsFromClosedCurves(IEnumerable<Curve> inputCurves)
      {
         using(DBObjectCollection curves = new DBObjectCollection())
         {
            foreach(Curve curve in inputCurves)
            {
               if(!curve.IsPlanar)
                  throw new ArgumentException("Non-planar curve rejected");
               curves.Add(curve);
            }
            return Region.CreateFromCurves(curves);
         }
      }

   }

   /// Code analysis 
   [AttributeUsage(AttributeTargets.ReturnValue, AllowMultiple = false, Inherited = true)]
   public sealed class CallerMustDisposeAttribute : Attribute
   {
   }
}
