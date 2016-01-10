using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Android.Gms.Common;
using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using TK.CustomMap;
using TK.CustomMap.Droid;
using TK.CustomMap.Overlays;
using Xamarin.Forms;
using Xamarin.Forms.Maps.Android;
using Xamarin.Forms.Platform.Android;
using Android.Gms.Location.Places;
using Xamarin.Forms.Maps;
using TK.CustomMap.Api.Google;
using TK.CustomMap.Utilities;

[assembly: ExportRenderer(typeof(TKCustomMap), typeof(TKCustomMapRenderer))]
namespace TK.CustomMap.Droid
{
      /// <summary>
      /// Android Renderer of <see cref="TK.CustomMap.TKCustomMap"/>
      /// </summary>
    public class TKCustomMapRenderer : MapRenderer, IOnMapReadyCallback
    {
        private bool _init = true;

        private readonly Dictionary<TKRoute, Polyline> _routes = new Dictionary<TKRoute, Polyline>();
        private readonly Dictionary<TKPolyline, Polyline> _polylines = new Dictionary<TKPolyline, Polyline>();
        private readonly Dictionary<TKCircle, Circle> _circles = new Dictionary<TKCircle, Circle>();
        private readonly Dictionary<TKPolygon, Polygon> _polygons = new Dictionary<TKPolygon, Polygon>();
        private readonly Dictionary<TKCustomMapPin, Marker> _markers = new Dictionary<TKCustomMapPin, Marker>();
        private Marker _selectedMarker;
        private bool _isDragging;
        private bool _firstUpdate = true;

        private GoogleMap _googleMap;

        private TKCustomMap FormsMap
        {
            get { return this.Element as TKCustomMap; }
        }

        /// <inheritdoc />
        protected override void OnElementChanged(ElementChangedEventArgs<View> e)
        {
            base.OnElementChanged(e);

            MapView mapView = this.Control as MapView;
            if (mapView == null) return;
            
            if (this.FormsMap != null && this._googleMap == null)
            {
                mapView.GetMapAsync(this);
                this.FormsMap.PropertyChanged += FormsMapPropertyChanged;

                if (e.OldElement == null)
                {
                    if (this.FormsMap.CustomPins != null)
                    {
                        this.FormsMap.CustomPins.CollectionChanged += OnCustomPinsCollectionChanged;
                    }

                }
            }
        }
        ///<inheritdoc/>
        protected override void OnLayout(bool changed, int l, int t, int r, int b)
        {
            base.OnLayout(changed, l, t, r, b);

            if (this._init)
            {
                this.MoveToCenter();
                this._init = false;
            }
        }
        /// <summary>
        /// When a property of the Forms map changed
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void FormsMapPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(this._googleMap == null) return;

            if (e.PropertyName == TKCustomMap.CustomPinsProperty.PropertyName)
            {
                this._firstUpdate = true;
                this.UpdatePins();
            }
            else if (e.PropertyName == TKCustomMap.SelectedPinProperty.PropertyName)
            {
                this.SetSelectedItem();
            }
            else if (e.PropertyName == TKCustomMap.MapCenterProperty.PropertyName)
            {
                this.MoveToCenter();
            }
            else if (e.PropertyName == TKCustomMap.PolylinesProperty.PropertyName)
            {
                this.UpdateLines();
            }
            else if (e.PropertyName == TKCustomMap.CirclesProperty.PropertyName)
            {
                this.UpdateCircles();
            }
            else if (e.PropertyName == TKCustomMap.PolygonsProperty.PropertyName)
            {
                this.UpdatePolygons();
            }
            else if (e.PropertyName == TKCustomMap.RoutesProperty.PropertyName)
            {
                this.UpdateRoutes();
            }
        }
        /// <summary>
        /// When the map is ready to use
        /// </summary>
        /// <param name="googleMap">The map instance</param>
        public void OnMapReady(GoogleMap googleMap)
        {
            this._googleMap = googleMap;
            
            this._googleMap.MarkerClick += OnMarkerClick;
            this._googleMap.MapClick += OnMapClick;
            this._googleMap.MapLongClick += OnMapLongClick;
            this._googleMap.MarkerDragEnd += OnMarkerDragEnd;
            this._googleMap.MarkerDrag += OnMarkerDrag;
            this._googleMap.CameraChange += OnCameraChange;
            this._googleMap.MarkerDragStart += OnMarkerDragStart;
            this._googleMap.InfoWindowClick += OnInfoWindowClick;
            
            this.MoveToCenter();
            this.UpdatePins();
            this.UpdateRoutes();
            this.UpdateLines();
            this.UpdateCircles();
            this.UpdatePolygons();
        }
        /// <summary>
        /// When the info window gets clicked
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnInfoWindowClick(object sender, GoogleMap.InfoWindowClickEventArgs e)
        {
            if (this.FormsMap.CalloutClickedCommand != null && this.FormsMap.CalloutClickedCommand.CanExecute(null))
            {
                this.FormsMap.CalloutClickedCommand.Execute(null);
            }
        }
        /// <summary>
        /// Dragging process
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnMarkerDrag(object sender, GoogleMap.MarkerDragEventArgs e)
        {
            var item = this._markers.SingleOrDefault(i => i.Value.Id.Equals(e.Marker.Id));
            if (item.Key == null) return;

            item.Key.Position = e.Marker.Position.ToPosition();
        }
        /// <summary>
        /// When a dragging starts
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnMarkerDragStart(object sender, GoogleMap.MarkerDragStartEventArgs e)
        {
            this._isDragging = true;
        }
        /// <summary>
        /// When the camera position changed
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnCameraChange(object sender, GoogleMap.CameraChangeEventArgs e)
        {
            this.FormsMap.MapCenter = e.Position.Target.ToPosition();
            base.OnCameraChange(e.Position);
        }
        /// <summary>
        /// When a pin gets clicked
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnMarkerClick(object sender, GoogleMap.MarkerClickEventArgs e)
        {
            if (this.FormsMap == null) return;
            var item = this._markers.SingleOrDefault(i => i.Value.Id.Equals(e.Marker.Id));
            if (item.Key == null) return;

            this._selectedMarker = e.Marker;
            this.FormsMap.SelectedPin = item.Key;
            if (item.Key.ShowCallout)
            {
                item.Value.ShowInfoWindow();
            }
        }
        /// <summary>
        /// When a drag of a marker ends
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnMarkerDragEnd(object sender, GoogleMap.MarkerDragEndEventArgs e)
        {
            this._isDragging = false;

            if (this.FormsMap == null) return;

            var pin = this._markers.SingleOrDefault(i => i.Value.Id.Equals(e.Marker.Id));
            if (pin.Key == null) return;

            if (this.FormsMap.PinDragEndCommand != null && this.FormsMap.PinDragEndCommand.CanExecute(pin.Key))
            {
                this.FormsMap.PinDragEndCommand.Execute(pin.Key);
            }
        }
        /// <summary>
        /// When a long click was performed on the map
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnMapLongClick(object sender, GoogleMap.MapLongClickEventArgs e)
        {
            if (this.FormsMap == null || this.FormsMap.MapLongPressCommand == null) return;

            var position = e.Point.ToPosition();

            if (this.FormsMap.MapLongPressCommand.CanExecute(position))
            {
                this.FormsMap.MapLongPressCommand.Execute(position);
            }
        }
        /// <summary>
        /// When the map got tapped
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnMapClick(object sender, GoogleMap.MapClickEventArgs e)
        {
            if (this.FormsMap == null || this.FormsMap.MapClickedCommand == null) return;

            var position = e.Point.ToPosition();

            if (this.FormsMap.Routes != null && this.FormsMap.RouteClickedCommand != null)
            {
                foreach(var route in this.FormsMap.Routes.Where(i => i.Selectable))
                {
                    var internalRoute = this._routes[route];

                    if (GmsPolyUtil.IsLocationOnPath(
                        position, 
                        internalRoute.Points.Select(i => i.ToPosition()), 
                        true,
                        (int)this._googleMap.CameraPosition.Zoom, 
                        this.FormsMap.MapCenter.Latitude))
                    {
                        if (this.FormsMap.RouteClickedCommand.CanExecute(route))
                        {
                            this.FormsMap.RouteClickedCommand.Execute(route);
                            return;
                        }
                    }
                }
            }
            
            if (this.FormsMap.MapClickedCommand.CanExecute(position))
            {
                this.FormsMap.MapClickedCommand.Execute(position);
            }
        }
        /// <summary>
        /// Updates the markers when a pin gets added or removed in the collection
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnCustomPinsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (TKCustomMapPin pin in e.NewItems)
                {
                    this.AddPin(pin);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (TKCustomMapPin pin in e.OldItems)
                {
                    if (!this.FormsMap.CustomPins.Contains(pin))
                    {
                        this.RemovePin(pin);
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var item in this._markers)
                {
                    item.Key.PropertyChanged -= OnPinPropertyChanged;
                }
                this._firstUpdate = true;
                this.UpdatePins();
            }
        }
        /// <summary>
        /// When a property of a pin changed
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private async void OnPinPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var pin = sender as TKCustomMapPin;
            if (pin == null) return;

            var marker = this._markers[pin];
            if (marker == null) return;

            switch (e.PropertyName)
            {
                case TKCustomMapPin.TitlePropertyName:
                    marker.Title = pin.Title;
                    break;
                case TKCustomMapPin.SubititlePropertyName:
                    marker.Snippet = pin.Subtitle;
                    break;
                case TKCustomMapPin.ImagePropertyName:
                    await this.UpdateImage(pin, marker);
                    break;
                case TKCustomMapPin.DefaultPinColorPropertyName:
                    await this.UpdateImage(pin, marker);
                    break;
                case TKCustomMapPin.PositionPropertyName:
                    if (!this._isDragging)
                    {
                        marker.Position = new LatLng(pin.Position.Latitude, pin.Position.Longitude);
                    }
                    break;
                case TKCustomMapPin.IsVisiblePropertyName:
                    marker.Visible = pin.IsVisible;
                    break;
            }
        }
        /// <summary>
        /// Collection of routes changed
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnLineCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (TKPolyline line in e.NewItems)
                {
                    this.AddLine(line);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (TKPolyline line in e.OldItems)
                {
                    if (!this.FormsMap.Polylines.Contains(line))
                    {
                        this._polylines[line].Remove();
                        line.PropertyChanged -= OnLinePropertyChanged;
                        this._polylines.Remove(line);
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                this.UpdateLines(false);
            }
        }
        /// <summary>
        /// A property of a route changed
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnLinePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var line = (TKPolyline)sender;

            if (e.PropertyName == TKPolyline.LineCoordinatesPropertyName)
            {
                if (line.LineCoordinates != null && line.LineCoordinates.Count > 1)
                {
                    this._polylines[line].Points = new List<LatLng>(line.LineCoordinates.Select(i => i.ToLatLng()));
                }
                else
                {
                    this._polylines[line].Points = null;
                }
            }
            else if (e.PropertyName == TKPolyline.ColorPropertyName)
            {
                this._polylines[line].Color = line.Color.ToAndroid().ToArgb();
            }
            else if (e.PropertyName == TKPolyline.LineWidthProperty)
            {
                this._polylines[line].Width = line.LineWidth;
            }
        }
        /// <summary>
        /// Creates all Markers on the map
        /// </summary>
        private void UpdatePins()
        {
            if (this._googleMap == null) return;

            this._googleMap.Clear();
            this._markers.Clear();

            var items = this.FormsMap.CustomPins;

            if (items == null || !items.Any()) return;

            foreach (var pin in items)
            {
                this.AddPin(pin);

                if (this._firstUpdate)
                {
                    pin.PropertyChanged += OnPinPropertyChanged;
                }
            }
            this._firstUpdate = false;

            if (this.FormsMap.PinsReadyCommand != null && this.FormsMap.PinsReadyCommand.CanExecute(this.FormsMap))
            {
                this.FormsMap.PinsReadyCommand.Execute(this.FormsMap);
            }
        }
        /// <summary>
        /// Adds a marker to the map
        /// </summary>
        /// <param name="pin">The Forms Pin</param>
        private async void AddPin(TKCustomMapPin pin)
        {
            var markerWithIcon = new MarkerOptions();
            markerWithIcon.SetPosition(new LatLng(pin.Position.Latitude, pin.Position.Longitude));

            if (!string.IsNullOrWhiteSpace(pin.Title))
                markerWithIcon.SetTitle(pin.Title);
            if (!string.IsNullOrWhiteSpace(pin.Subtitle))
                markerWithIcon.SetSnippet(pin.Subtitle);

            await this.UpdateImage(pin, markerWithIcon);
            markerWithIcon.Draggable(pin.IsDraggable);
            markerWithIcon.Visible(pin.IsVisible);

            this._markers.Add(pin, this._googleMap.AddMarker(markerWithIcon));
        }
        /// <summary>
        /// Remove a pin from the map and the internal dictionary
        /// </summary>
        /// <param name="pin">The pin to remove</param>
        private void RemovePin(TKCustomMapPin pin)
        {
            var item = this._markers[pin];
            if(item == null) return;

            if (this._selectedMarker != null)
            {
                if (item.Id.Equals(this._selectedMarker.Id))
                {
                    this.FormsMap.SelectedPin = null;
                }
            }

            item.Remove();
            pin.PropertyChanged -= OnPinPropertyChanged;
            this._markers.Remove(pin);
        }
        /// <summary>
        /// Set the selected item on the map
        /// </summary>
        private void SetSelectedItem()
        {
            if (this._selectedMarker != null)
            {
                this._selectedMarker.HideInfoWindow();
                this._selectedMarker = null;
            }
            if (this.FormsMap.SelectedPin != null)
            {
                var selectedPin = this._markers[this.FormsMap.SelectedPin];
                this._selectedMarker = selectedPin;
                if (this.FormsMap.SelectedPin.ShowCallout)
                {
                    selectedPin.ShowInfoWindow();
                }
                if (this.FormsMap.PinSelectedCommand != null && this.FormsMap.PinSelectedCommand.CanExecute(null))
                {
                    this.FormsMap.PinSelectedCommand.Execute(null);
                }
            }
        }
        /// <summary>
        /// Move the google map to the map center
        /// </summary>
        private void MoveToCenter()
        {
            if (this._googleMap == null) return;

            if (!this.FormsMap.MapCenter.Equals(this._googleMap.CameraPosition.Target.ToPosition()))
            {
                var cameraUpdate = CameraUpdateFactory.NewLatLng(this.FormsMap.MapCenter.ToLatLng());

                if (this.FormsMap.AnimateMapCenterChange && !this._init)
                {
                    this._googleMap.AnimateCamera(cameraUpdate);
                }
                else
                {
                    this._googleMap.MoveCamera(cameraUpdate);
                }
            }
        }
        /// <summary>
        /// Creates the routes on the map
        /// </summary>
        private void UpdateLines(bool firstUpdate = true)
        {
            if (this._googleMap == null) return;

            foreach (var i in this._polylines)
            {
                i.Key.PropertyChanged -= OnLinePropertyChanged;
                i.Value.Remove();
            }
            this._polylines.Clear();

            if (this.FormsMap.Polylines != null)
            {
                foreach (var line in this.FormsMap.Polylines)
                {
                    this.AddLine(line);
                }

                if (firstUpdate)
                {
                    var observAble = this.FormsMap.Polylines as ObservableCollection<TKRoute>;
                    if (observAble != null)
                    {
                        observAble.CollectionChanged += OnLineCollectionChanged;
                    }
                }
            }
        }
        /// <summary>
        /// Updates all circles
        /// </summary>
        private void UpdateCircles(bool firstUpdate = true)
        {
            if (this._googleMap == null) return;

            foreach (var i in this._circles)
            {
                i.Key.PropertyChanged -= CirclePropertyChanged;
                i.Value.Remove();
            }
            if (this.FormsMap.Circles != null)
            {
                foreach (var circle in this.FormsMap.Circles)
                {
                    this.AddCircle(circle);
                }
                if (firstUpdate)
                {
                    var observAble = this.FormsMap.Circles as ObservableCollection<TKCircle>;
                    if (observAble != null)
                    {
                        observAble.CollectionChanged += CirclesCollectionChanged;
                    }
                }
            }
        }
        /// <summary>
        /// Creates the polygones on the map
        /// </summary>
        /// <param name="firstUpdate">If the collection updates the first time</param>
        private void UpdatePolygons(bool firstUpdate = true)
        {
            if (this._googleMap == null) return;

            foreach (var i in this._polygons)
            {
                i.Key.PropertyChanged -= OnPolygonPropertyChanged;
                i.Value.Remove();
            }
            if (this.FormsMap.Polygons != null)
            {
                foreach (var i in this.FormsMap.Polygons)
                {
                    this.AddPolygon(i);
                }
                if (firstUpdate)
                {
                    var observAble = this.FormsMap.Polygons as ObservableCollection<TKPolygon>;
                    if (observAble != null)
                    {
                        observAble.CollectionChanged += OnPolygonsCollectionChanged;
                    }
                }
            }
        }
        /// <summary>
        /// Create all routes
        /// </summary>
        /// <param name="firstUpdate">If first update of collection or not</param>
        private void UpdateRoutes(bool firstUpdate = true)
        {
            if (this._googleMap == null) return;

            foreach (var i in this._routes)
            {
                i.Key.PropertyChanged -= OnRoutePropertyChanged;
                i.Value.Remove();
            }
            if (this.FormsMap.Routes != null)
            {
                foreach (var i in this.FormsMap.Routes)
                {
                    this.AddRoute(i);
                }
            }
            if (firstUpdate)
            {
                var observAble = this.FormsMap.Routes as ObservableCollection<TKRoute>;
                if (observAble != null)
                {
                    observAble.CollectionChanged += OnRouteCollectionChanged;
                }
            }
        }
        /// <summary>
        /// When the collection of routes changed
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnRouteCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (TKRoute route in e.NewItems)
                {
                    this.AddRoute(route);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (TKRoute route in e.OldItems)
                {
                    if (!this.FormsMap.Routes.Contains(route))
                    {
                        this._routes[route].Remove();
                        route.PropertyChanged -= OnRoutePropertyChanged;
                        this._routes.Remove(route);
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                this.UpdateRoutes(false);
            }
        }
        /// <summary>
        /// When a property of a route changed
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnRoutePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var route = (TKRoute)sender;

            if (e.PropertyName == TKRoute.SourceProperty || 
                e.PropertyName == TKRoute.DestinationProperty || 
                e.PropertyName == TKRoute.TravelModelProperty)
            {
                route.PropertyChanged -= OnRoutePropertyChanged;
                this._routes[route].Remove();
                this._routes.Remove(route);

                this.AddRoute(route);
            }
            else if (e.PropertyName == TKPolyline.ColorPropertyName)
            {
                this._routes[route].Color = route.Color.ToAndroid().ToArgb();
            }
            else if (e.PropertyName == TKPolyline.LineWidthProperty)
            {
                this._routes[route].Width = route.LineWidth;
            }
        }
        /// <summary>
        /// When the polygon collection changed
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnPolygonsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (TKPolygon poly in e.NewItems)
                {
                    this.AddPolygon(poly);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (TKPolygon poly in e.OldItems)
                {
                    if (!this.FormsMap.Polygons.Contains(poly))
                    {
                        this._polygons[poly].Remove();
                        poly.PropertyChanged -= OnPolygonPropertyChanged;
                        this._polygons.Remove(poly);
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                this.UpdatePolygons(false);
            }
        }
        /// <summary>
        /// Adds a polygon to the map
        /// </summary>
        /// <param name="polygon">The polygon to add</param>
        private void AddPolygon(TKPolygon polygon)
        {
            polygon.PropertyChanged += OnPolygonPropertyChanged;

            var polygonOptions = new PolygonOptions();

            if (polygon.Coordinates != null && polygon.Coordinates.Any())
            {
                polygonOptions.Add(polygon.Coordinates.Select(i => i.ToLatLng()).ToArray());
            }
            if (polygon.Color != Color.Default)
            {
                polygonOptions.InvokeFillColor(polygon.Color.ToAndroid().ToArgb());
            }
            if (polygon.StrokeColor != Color.Default)
            {
                polygonOptions.InvokeStrokeColor(polygon.StrokeColor.ToAndroid().ToArgb());
            }
            polygonOptions.InvokeStrokeWidth(polygonOptions.StrokeWidth);

            this._polygons.Add(polygon, this._googleMap.AddPolygon(polygonOptions));
        }
        /// <summary>
        /// When a property of a <see cref="TKPolygon"/> changed
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnPolygonPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var tkPolygon = (TKPolygon)sender;

            switch (e.PropertyName)
            {
                case TKPolygon.CoordinatesPropertyName:
                    this._polygons[tkPolygon].Points = tkPolygon.Coordinates.Select(i => i.ToLatLng()).ToList();
                    break;
                case TKPolygon.ColorPropertyName:
                    this._polygons[tkPolygon].FillColor = tkPolygon.Color.ToAndroid().ToArgb();
                    break;
                case TKPolygon.StrokeColorPropertyName:
                    this._polygons[tkPolygon].StrokeColor = tkPolygon.StrokeColor.ToAndroid().ToArgb();
                    break;
                case TKPolygon.StrokeWidthPropertyName:
                    this._polygons[tkPolygon].StrokeWidth = tkPolygon.StrokeWidth;
                    break;
            }
        }
        /// <summary>
        /// When the circle collection changed
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void CirclesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if(e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (TKCircle circle in e.NewItems)
                {
                    this.AddCircle(circle);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (TKCircle circle in e.OldItems)
                {
                    if (!this.FormsMap.Circles.Contains(circle))
                    {
                        circle.PropertyChanged -= CirclePropertyChanged;
                        this._circles[circle].Remove();
                        this._circles.Remove(circle);
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                this.UpdateCircles(false);
            }
        }
        /// <summary>
        /// Adds a circle to the map
        /// </summary>
        /// <param name="circle">The circle to add</param>
        private void AddCircle(TKCircle circle)
        {
            circle.PropertyChanged += CirclePropertyChanged;

            var circleOptions = new CircleOptions();

            circleOptions.InvokeRadius(circle.Radius);
            circleOptions.InvokeCenter(circle.Center.ToLatLng());

            if (circle.Color != Color.Default)
            {
                circleOptions.InvokeFillColor(circle.Color.ToAndroid().ToArgb());
            }
            if (circle.StrokeColor != Color.Default)
            {
                circleOptions.InvokeStrokeColor(circle.StrokeColor.ToAndroid().ToArgb());
            }
            circleOptions.InvokeStrokeWidth(circle.StrokeWidth);
            this._circles.Add(circle, this._googleMap.AddCircle(circleOptions));
        }
        /// <summary>
        /// When a property of a <see cref="TKCircle"/> changed
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void CirclePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var tkCircle = (TKCircle)sender;
            var circle = this._circles[tkCircle];

            switch(e.PropertyName)
            {
                case TKCircle.RadiusPropertyName:
                    circle.Radius = tkCircle.Radius;
                    break;
                case TKCircle.CenterPropertyName:
                    circle.Center = tkCircle.Center.ToLatLng();
                    break;
                case TKCircle.ColorPropertyName:
                    circle.FillColor = tkCircle.Color.ToAndroid().ToArgb();
                    break;
                case TKCircle.StrokeColorPropertyName:
                    circle.StrokeColor = tkCircle.StrokeColor.ToAndroid().ToArgb();
                    break;
            }
        }
        /// <summary>
        /// Adds a route to the map
        /// </summary>
        /// <param name="line">The route to add</param>
        private void AddLine(TKPolyline line)
        {
            line.PropertyChanged += OnLinePropertyChanged;
            
            var polylineOptions = new PolylineOptions();
            if (line.Color != Color.Default)
            {
                polylineOptions.InvokeColor(line.Color.ToAndroid().ToArgb());
            }
            if (line.LineWidth > 0)
            {
                polylineOptions.InvokeWidth(line.LineWidth);
            }

            if (line.LineCoordinates != null)
            {
                polylineOptions.Add(line.LineCoordinates.Select(i => i.ToLatLng()).ToArray());
            }

            this._polylines.Add(line, this._googleMap.AddPolyline(polylineOptions));
        }
        /// <summary>
        /// Calculates and adds the route to the map
        /// </summary>
        /// <param name="route">The route to add</param>
        private async void AddRoute(TKRoute route)
        {
            route.PropertyChanged += OnRoutePropertyChanged;

            GmsDirectionResult routeData = null;
            try
            {
                routeData = await GmsDirection.Instance.CalculateRoute(route.Source, route.Destination, route.TravelMode.ToGmsTravelMode());
                
                if (routeData == null || routeData.Routes == null) return;

                var r = routeData.Routes.FirstOrDefault();
                if (r == null || r.Polyline.Positions == null || !r.Polyline.Positions.Any()) return;

                this.SetRouteData(route, r);

                var routeOptions = new PolylineOptions();

                if (route.Color != Color.Default)
                {
                    routeOptions.InvokeColor(route.Color.ToAndroid().ToArgb());
                }
                if (route.LineWidth > 0)
                {
                    routeOptions.InvokeWidth(route.LineWidth);
                }
                routeOptions.Add(r.Polyline.Positions.Select(i => i.ToLatLng()).ToArray());

                this._routes.Add(route, this._googleMap.AddPolyline(routeOptions));

                if (this.FormsMap.RouteCalculationFinishedCommand != null && this.FormsMap.RouteCalculationFinishedCommand.CanExecute(route))
                {
                    this.FormsMap.RouteCalculationFinishedCommand.Execute(route);
                }
            }
            finally
            {
                if ((routeData == null || routeData.Status != GmsDirectionResultStatus.Ok) && this.FormsMap.RouteCalculationFailedCommand != null)
                {
                    if (this.FormsMap.RouteCalculationFailedCommand.CanExecute(route))
                    {
                        this.FormsMap.RouteCalculationFailedCommand.Execute(route);
                    }
                }
            }
        }
        /// <summary>
        /// Sets the route calculation data
        /// </summary>
        /// <param name="route">The PCL route</param>
        /// <param name="routeResult">The rourte api result</param>
        private void SetRouteData(TKRoute route, GmsRouteResult routeResult)
        {
            var latLngBounds = new LatLngBounds(
                    new LatLng(routeResult.Bounds.SouthWest.Latitude, routeResult.Bounds.SouthWest.Longitude),
                    new LatLng(routeResult.Bounds.NorthEast.Latitude, routeResult.Bounds.NorthEast.Longitude));

            var apiSteps = routeResult.Legs.First().Steps;
            var steps = new TKRouteStep[apiSteps.Count()];
            var routeFunctions = (IRouteFunctions)route;

            
            for (int i = 0; i < steps.Length; i++)
            {
                steps[i] = new TKRouteStep();
                var stepFunctions = (IRouteStepFunctions)steps[i];
                var apiStep = apiSteps.ElementAt(i);

                stepFunctions.SetDistance(apiStep.Distance.Value);
                stepFunctions.SetInstructions(apiStep.HtmlInstructions);
            }
            routeFunctions.SetSteps(steps);
            routeFunctions.SetDistance(routeResult.Legs.First().Distance.Value);
            routeFunctions.SetTravelTime(routeResult.Legs.First().Duration.Value);
            routeFunctions.SetBounds(
                MapSpan.FromCenterAndRadius(
                    latLngBounds.Center.ToPosition(),
                    Distance.FromKilometers(
                        new Position(latLngBounds.Southwest.Latitude, latLngBounds.Southwest.Longitude)
                        .DistanceTo(
                            new Position(latLngBounds.Northeast.Latitude, latLngBounds.Northeast.Longitude)))));

        }
        /// <summary>
        /// Updates the image of a pin
        /// </summary>
        /// <param name="pin">The forms pin</param>
        /// <param name="markerOptions">The native marker options</param>
        private async Task UpdateImage(TKCustomMapPin pin, MarkerOptions markerOptions)
        {
            BitmapDescriptor bitmap;
            try
            {
                if (pin.Image != null)
                {
                    var icon = await new ImageLoaderSourceHandler().LoadImageAsync(pin.Image, this.Context);
                    bitmap = BitmapDescriptorFactory.FromBitmap(icon);
                }
                else
                {
                    if (pin.DefaultPinColor != Color.Default)
                    {
                        bitmap = BitmapDescriptorFactory.DefaultMarker(pin.DefaultPinColor.ToAndroid().GetHue());
                    }
                    else
                    {
                        bitmap = BitmapDescriptorFactory.DefaultMarker();
                    }
                }
            }
            catch (Exception)
            {
                bitmap = BitmapDescriptorFactory.DefaultMarker();
            }
            markerOptions.SetIcon(bitmap);
        }
        /// <summary>
        /// Updates the image on a marker
        /// </summary>
        /// <param name="pin">The forms pin</param>
        /// <param name="marker">The native marker</param>
        private async Task UpdateImage(TKCustomMapPin pin, Marker marker)
        {
            BitmapDescriptor bitmap;
            try
            {
                if (pin.Image != null)
                {
                    var icon = await new ImageLoaderSourceHandler().LoadImageAsync(pin.Image, this.Context);
                    bitmap = BitmapDescriptorFactory.FromBitmap(icon);
                }
                else
                {
                    if (pin.DefaultPinColor != Color.Default)
                    {
                        bitmap = BitmapDescriptorFactory.DefaultMarker((float)pin.DefaultPinColor.Hue);
                    }
                    else
                    {
                        bitmap = BitmapDescriptorFactory.DefaultMarker();
                    }
                }
            }
            catch (Exception)
            {
                bitmap = BitmapDescriptorFactory.DefaultMarker();
            }
            marker.SetIcon(bitmap);
        }

        
    }
}