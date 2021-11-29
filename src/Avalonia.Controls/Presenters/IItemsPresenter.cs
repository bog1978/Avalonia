using System.Collections.Generic;
using Avalonia.Controls.Generators;

#nullable enable

namespace Avalonia.Controls.Presenters
{
    /// <summary>
    /// Represents a control which presents the list of items inside the template of an
    /// <see cref="ItemsControl"/>.
    /// </summary>
    public interface IItemsPresenter : IPresenter
    {
        /// <summary>
        /// Gets the container generator for the presenter.
        /// </summary>
        IItemContainerGenerator? ItemContainerGenerator { get; }

        /// <summary>
        /// Gets an <see cref="ItemsSourceView"/> containing the items to display.
        /// </summary>
        ItemsSourceView? ItemsView { get; }

        /// <summary>
        /// Gets the <see cref="IPanel"/> on which the item containers are hosted.
        /// </summary>
        IPanel? Panel { get; }

        /// <summary>
        /// Gets the currently realized elements.
        /// </summary>
        IEnumerable<IControl> RealizedElements { get; }

        /// <summary>
        /// Gets the container for the specified index in the item source, if realized.
        /// </summary>
        /// <param name="index">The index in the item source.</param>
        /// <returns>
        /// The container; or null if not realized or index is invalid.
        /// </returns>
        IControl? GetContainerForIndex(int index);

        /// <summary>
        /// Gets the index in the item source of the specified container.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <returns>
        /// The index in the item source; or -1 if the control is not a container belonging
        /// to this presenter.
        /// </returns>
        int GetContainerIndex(IControl container);

        /// <summary>
        /// Scrolls the requested index into view.
        /// </summary>
        /// <param name="index">The index in the item source.</param>
        void ScrollIntoView(int index);

        /// <summary>
        /// Called by an <see cref="IVirtualizingPanel"/> when a container is realized.
        /// </summary>
        /// <param name="container">The container that was realized.</param>
        /// <param name="index">The index of the container in the data source.</param>
        /// <param name="item">The item that will be displayed in the container.</param>
        void ContainerRealized(IControl container, int index, object? item);

        /// <summary>
        /// Called by an <see cref="IVirtualizingPanel"/> when a container is unrealized.
        /// </summary>
        /// <param name="container">The container that was realized.</param>
        /// <param name="index">The index of the container in the data source.</param>
        void ContainerUnrealized(IControl container, int index);

        /// <summary>
        /// Called by an <see cref="IVirtualizingPanel"/> when the index of a realized container
        /// has changed.
        /// </summary>
        /// <param name="container">The container that was realized.</param>
        /// <param name="oldIndex">The old index of the container in the data source.</param>
        /// <param name="newIndex">The new index of the container in the data source.</param>
        void ContainerIndexChanged(IControl container, int oldIndex, int newIndex);
    }
}
