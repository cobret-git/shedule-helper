using System;

namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// A single entry in the app's breadcrumb trail. Captures only the label to display and a
    /// closure that re-runs the original navigation - the ViewModel/page/context types (and, for
    /// child segments, the context value) are already baked into that closure by the navigation
    /// service at the point they were still known statically, so re-activating a segment needs
    /// no runtime type matching or reflection.
    /// </summary>
    public sealed class BreadcrumbItem
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="BreadcrumbItem"/> class.
        /// </summary>
        /// <param name="label">The text displayed for this breadcrumb segment.</param>
        /// <param name="reactivate">Re-runs the navigation this segment represents.</param>
        public BreadcrumbItem(string label, Action reactivate)
        {
            Label = label;
            Reactivate = reactivate;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The text displayed for this breadcrumb segment.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Re-runs the navigation this segment represents. Intended to be invoked only by the
        /// navigation service that created this item.
        /// </summary>
        public Action Reactivate { get; }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public override string ToString() => Label;

        #endregion
    }
}
