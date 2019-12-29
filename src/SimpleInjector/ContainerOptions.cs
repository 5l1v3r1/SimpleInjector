﻿// Copyright (c) Simple Injector Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace SimpleInjector
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using SimpleInjector.Advanced;
    using SimpleInjector.Diagnostics;

    /// <summary>
    /// Delegate that allows intercepting calls to <see cref="Container.GetInstance"/> and
    /// <see cref="InstanceProducer.GetInstance"/>.
    /// </summary>
    /// <param name="context">Contextual information about the to be created object.</param>
    /// <param name="instanceProducer">A delegate that produces the actual instance according to its
    /// lifestyle settings.</param>
    /// <returns>The instance that is returned from <paramref name="instanceProducer"/> or an intercepted instance.</returns>
    public delegate object ResolveInterceptor(InitializationContext context, Func<object> instanceProducer);

    /// <summary>Configuration options for the <see cref="SimpleInjector.Container">Container</see>.</summary>
    /// <example>
    /// The following example shows the typical usage of the <b>ContainerOptions</b> class.
    /// <code lang="cs"><![CDATA[
    /// var container = new Container();
    ///
    /// container.Register<ITimeProvider, DefaultTimeProvider>();
    ///
    /// // Use of ContainerOptions class here.
    /// container.Options.AllowOverridingRegistrations = true;
    ///
    /// // Replaces the previous registration of ITimeProvider
    /// container.Register<ITimeProvider, CustomTimeProvider>();
    /// ]]></code>
    /// </example>
    [DebuggerDisplay("{" + nameof(ContainerOptions.DebuggerDisplayDescription) + ", nq}")]
    public class ContainerOptions : ApiObject
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private EventHandler<ContainerLockingEventArgs>? containerLocking;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private IConstructorResolutionBehavior resolutionBehavior;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private IDependencyInjectionBehavior injectionBehavior;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private IPropertySelectionBehavior propertyBehavior;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ILifestyleSelectionBehavior lifestyleBehavior;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Lifestyle defaultLifestyle = Lifestyle.Transient;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ScopedLifestyle? defaultScopedLifestyle;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool resolveUnregisteredConcreteTypes = true;

        internal ContainerOptions(Container container)
        {
            Requires.IsNotNull(container, nameof(container));

            this.Container = container;
            this.resolutionBehavior = new DefaultConstructorResolutionBehavior();
            this.injectionBehavior = new DefaultDependencyInjectionBehavior(container);
            this.propertyBehavior = new DefaultPropertySelectionBehavior();
            this.lifestyleBehavior = new DefaultLifestyleSelectionBehavior(this);
        }

        /// <summary>
        /// Occurs just before the container is about to be locked, giving the developer a last change to
        /// interact and change the unlocked container before it is sealed for further modifications. Locking
        /// typically occurs by a call to <b>Container.GetInstance</b>, <b>Container.Verify</b>, or any other
        /// method that causes the construction and resolution of registered instances.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <b>ContainerLocking</b> event is called exactly once by the container, allowing a developer to
        /// register types, hook unregistered type resolution events that need to be applied last, or see
        /// who is responsible for locking the container.
        /// </para>
        /// <para>
        /// A registered event handler delegate is allowed to make a call that locks the container, e.g.
        /// calling <b>Container.GetInstance</b>; this will not cause any new <b>ContainerLocking</b> event to
        /// be raised. Doing so, however, is not advised, as that might cause any following executed handlers
        /// to break, in case they require an unlocked container.
        /// </para>
        /// </remarks>
        public event EventHandler<ContainerLockingEventArgs> ContainerLocking
        {
            add
            {
                this.Container.ThrowWhenContainerIsLockedOrDisposed();

                this.containerLocking += value;
            }

            remove
            {
                this.Container.ThrowWhenContainerIsLockedOrDisposed();

                this.containerLocking -= value;
            }
        }

        /// <summary>
        /// Gets the container to which this <b>ContainerOptions</b> instance belongs to.
        /// </summary>
        /// <value>The current <see cref="SimpleInjector.Container">Container</see>.</value>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public Container Container { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the container allows overriding registrations. The default
        /// is false.
        /// </summary>
        /// <value>The value indicating whether the container allows overriding registrations.</value>
        public bool AllowOverridingRegistrations { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the container should suppress checking for lifestyle
        /// mismatches (see: https://simpleinjector.org/dialm) when a component is resolved. The default
        /// is false. This setting will have no effect when <see cref="EnableAutoVerification"/> is true.
        /// </summary>
        /// <value>The value indicating whether the container should suppress checking for lifestyle
        /// mismatches.</value>
        public bool SuppressLifestyleMismatchVerification { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the container should automatically trigger verification
        /// and diagnostics of its configuration when the first service is resolved (e.g. the first call to
        /// GetInstance). The behavior is identical to calling <see cref="Container.Verify()">Verify()</see>
        /// manually. The default is false.
        /// </summary>
        /// <value>The value indicating whether the container should automatically trigger verification.</value>
        public bool EnableAutoVerification { get; set; }

        /// <summary>Gets or sets a value indicating whether.
        /// This method is deprecated. Changing its value will have no effect.</summary>
        /// <value>The value indicating whether the container will return an empty collection.</value>
        [Obsolete("This method is not used any longer. Setting it has no effect. " +
            "Please register collections explicitly instead. " +
            "Will be removed in version 5.0.",
            error: true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ResolveUnregisteredCollections { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether all the containers in the current AppDomain should throw
        /// exceptions that contain fully qualified type name. The default is <c>false</c> which means
        /// the type's namespace is omitted.
        /// </summary>
        /// <value>The value indicating whether exception message should emit full type names.</value>
        public bool UseFullyQualifiedTypeNames
        {
            get { return StringResources.UseFullyQualifiedTypeNames; }
            set { StringResources.UseFullyQualifiedTypeNames = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the container should resolve unregistered concrete types.
        /// The default value is <code>true</code>. Consider changing the value to <code>false</code> to prevent
        /// accidental creation of types you haven't registered explicitly.
        /// </summary>
        /// <value>The value indicating whether the container should resolve unregistered concrete types.</value>
        /// <exception cref="InvalidOperationException">
        /// Thrown when this container instance is locked and can not be altered.
        /// </exception>
        public bool ResolveUnregisteredConcreteTypes
        {
            get
            {
                return this.resolveUnregisteredConcreteTypes;
            }

            set
            {
                this.Container.ThrowWhenContainerIsLockedOrDisposed();

                this.resolveUnregisteredConcreteTypes = value;
            }
        }

        /// <summary>
        /// Gets or sets the constructor resolution behavior. By default, the container only supports types
        /// that have a single public constructor.
        /// </summary>
        /// <value>The constructor resolution behavior.</value>
        /// <exception cref="NullReferenceException">Thrown when the supplied value is a null reference.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the container already contains registrations.
        /// </exception>
        public IConstructorResolutionBehavior ConstructorResolutionBehavior
        {
            get
            {
                return this.resolutionBehavior;
            }

            set
            {
                Requires.IsNotNull(value, nameof(value));

                this.ThrowWhenContainerHasRegistrations(nameof(this.ConstructorResolutionBehavior));

                this.resolutionBehavior = value;
            }
        }

        /// <summary>Gets or sets the dependency injection behavior.</summary>
        /// <value>The constructor injection behavior.</value>
        /// <exception cref="NullReferenceException">Thrown when the supplied value is a null reference.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the container already contains registrations.
        /// </exception>
        public IDependencyInjectionBehavior DependencyInjectionBehavior
        {
            get
            {
                return this.injectionBehavior;
            }

            set
            {
                Requires.IsNotNull(value, nameof(value));

                this.ThrowWhenContainerHasRegistrations(nameof(this.DependencyInjectionBehavior));

                this.injectionBehavior = value;
            }
        }

        /// <summary>
        /// Gets or sets the property selection behavior. The container's default behavior is to do no
        /// property injection.
        /// </summary>
        /// <value>The property selection behavior.</value>
        /// <exception cref="NullReferenceException">Thrown when the supplied value is a null reference.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the container already contains registrations.
        /// </exception>
        public IPropertySelectionBehavior PropertySelectionBehavior
        {
            get
            {
                return this.propertyBehavior;
            }

            set
            {
                Requires.IsNotNull(value, nameof(value));

                this.ThrowWhenContainerHasRegistrations(nameof(this.PropertySelectionBehavior));

                this.propertyBehavior = value;
            }
        }

        /// <summary>
        /// Gets or sets the lifestyle selection behavior. The container's default behavior is to make
        /// registrations using the <see cref="Lifestyle.Transient"/> lifestyle.</summary>
        /// <value>The lifestyle selection behavior.</value>
        /// <exception cref="NullReferenceException">Thrown when the supplied value is a null reference.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the container already contains registrations.
        /// </exception>
        public ILifestyleSelectionBehavior LifestyleSelectionBehavior
        {
            get
            {
                return this.lifestyleBehavior;
            }

            set
            {
                Requires.IsNotNull(value, nameof(value));

                this.ThrowWhenContainerHasRegistrations(nameof(this.LifestyleSelectionBehavior));

                this.lifestyleBehavior = value;
            }
        }

        /// <summary>
        /// Gets or sets the default lifestyle that the container will use when a registration is
        /// made when no lifestyle is supplied.</summary>
        /// <value>The default lifestyle.</value>
        /// <exception cref="NullReferenceException">Thrown when the supplied value is a null reference.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the container already contains registrations.
        /// </exception>
        public Lifestyle DefaultLifestyle
        {
            get
            {
                return this.defaultLifestyle;
            }

            set
            {
                Requires.IsNotNull(value, nameof(value));

                this.ThrowWhenContainerHasRegistrations(nameof(this.DefaultLifestyle));

                this.defaultLifestyle = value;
            }
        }

        /// <summary>
        /// Gets or sets the default scoped lifestyle that the container should use when a registration is
        /// made using <see cref="Lifestyle.Scoped">Lifestyle.Scoped</see>.</summary>
        /// <value>The default scoped lifestyle.</value>
        /// <exception cref="NullReferenceException">Thrown when the supplied value is a null reference.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the container already contains registrations.
        /// </exception>
        public ScopedLifestyle? DefaultScopedLifestyle
        {
            get
            {
                return this.defaultScopedLifestyle;
            }

            set
            {
                Requires.IsNotNull(value, nameof(value));

                if (object.ReferenceEquals(value, Lifestyle.Scoped))
                {
                    throw new ArgumentException(
                        StringResources.DefaultScopedLifestyleCanNotBeSetWithLifetimeScoped(),
                        nameof(value));
                }

                this.ThrowWhenContainerHasRegistrations(nameof(this.DefaultScopedLifestyle));

                this.defaultScopedLifestyle = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the container will use dynamic assemblies for compilation.
        /// By default, this value is <b>true</b> for the first few containers that are created in an AppDomain
        /// and <b>false</b> for all other containers. You can set this value explicitly to <b>false</b>
        /// to prevent the use of dynamic assemblies or you can set this value explicitly to <b>true</b> to
        /// force more container instances to use dynamic assemblies. Note that creating an infinite number
        /// of <see cref="SimpleInjector.Container">Container</see> instances (for instance one per web request)
        /// with this property set to <b>true</b> will result in a memory leak; dynamic assemblies take up
        /// memory and will only be unloaded when the AppDomain is unloaded.
        /// </summary>
        /// <value>A boolean indicating whether the container should use a dynamic assembly for compilation.
        /// </value>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public bool EnableDynamicAssemblyCompilation { get; set; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal int MaximumNumberOfNodesPerDelegate { get; set; } = 350;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal string DebuggerDisplayDescription => this.ToString();

        // This property enables a hidden hook to allow to get notified just before expression trees get
        // compiled. It isn't used internally, but enables debugging in case compiling expressions crashes
        // the process (which has happened in the past). A user can add the hook using reflection to find out
        // which expression crashes the system. This property is internal, its not part of the official API,
        // and we might remove it again in the future.
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal Action<Expression> ExpressionCompiling { get; set; } = _ => { };

        internal HashSet<Type> SuppressedDisposableBaseTypes { get; } = new HashSet<Type>();

        /// <summary>
        /// Registers an <see cref="ResolveInterceptor"/> delegate that allows intercepting calls to
        /// <see cref="SimpleInjector.Container.GetInstance">GetInstance</see> and
        /// <see cref="InstanceProducer.GetInstance()"/>.
        /// </summary>
        /// <remarks>
        /// If multiple registered <see cref="ResolveInterceptor"/> instances must be applied, they will be
        /// applied/wrapped in the order of registration, i.e. the first registered interceptor will call the
        /// original instance producer delegate, the second interceptor will call the first interceptor, etc.
        /// The last registered interceptor will become the outermost method in the chain and will be called
        /// first.
        /// </remarks>
        /// <param name="interceptor">The <see cref="ResolveInterceptor"/> delegate to register.</param>
        /// <param name="predicate">The predicate that will be used to check whether the given delegate must
        /// be applied to a registration or not. The given predicate will be called once for each registration
        /// in the container.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when either the <paramref name="interceptor"/> or <paramref name="predicate"/> are
        /// null references.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when this container instance is locked and can not be altered.
        /// </exception>
        /// <example>
        /// The following example shows the usage of the <see cref="RegisterResolveInterceptor" /> method:
        /// <code lang="cs"><![CDATA[
        /// var container = new Container();
        ///
        /// container.Options.RegisterResolveInterceptor((context, producer) =>
        ///     {
        ///         object instance = producer.Invoke();
        ///         Console.WriteLine(instance.GetType().Name + " resolved for " + context.Producer.ServiceType.Name);
        ///         return instance;
        ///     },
        ///     context => context.Producer.ServiceType.Name.EndsWith("Controller"));
        ///
        /// container.Register<IHomeViewModel, HomeViewModel>();
        /// container.Register<IUserRepository, SqlUserRepository>();
        ///
        /// // This line will write "HomeViewModel resolved for IHomeViewModel" to the console.
        /// container.GetInstance<IHomeViewModel>();
        /// ]]></code>
        /// </example>
        public void RegisterResolveInterceptor(
            ResolveInterceptor interceptor, Predicate<InitializationContext> predicate)
        {
            Requires.IsNotNull(interceptor, nameof(interceptor));
            Requires.IsNotNull(predicate, nameof(predicate));

            this.Container.ThrowWhenContainerIsLockedOrDisposed();

            this.Container.RegisterResolveInterceptor(interceptor, predicate);
        }

        /// <summary>
        /// Suppresses the <see cref="DiagnosticType.DisposableTransientComponent"/> on all types that derive
        /// from the supplied <paramref name="baseType"/> in case the derived type does not override the
        /// 'protected virtual void Dispose(bool)' method that is defined on the base type.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Framework base classes sometimes implement <see cref="IDisposable"/> and implement a 'protected
        /// virtual void Dispose(bool)' method for application developers to override, while leaving the base
        /// class's default implementation empty. Frameworks such as MVC and SignalR apply this design.
        /// </para>
        /// <para>
        /// This design, however, causes a problem when derivates are registered with Simple Injector, because
        /// Simple Injector does not disposes of transient components. This causes Simple Injector's
        /// diagnostics to warn about the registration of this undisposed (see https://simpleinjector.org/diadt)
        /// transient component. In practice, however, not disposing the component only causes a problem in
        /// case the application developer overrides Dispose(bool) in the derived type, as the base Dispose
        /// operation is a no-op.
        /// </para>
        /// <para>
        /// This <see cref="SuppressDisposableTransientVerificationWhenDisposeIsNotOverriddenFrom"/> method
        /// exists to mitigate this problem. It suppresses the warning on derivatives that do not override
        /// <c>Dispose</c> from the base class.
        /// </para>
        /// </remarks>
        /// <param name="baseType">The base type that defines a 'protected virtual void Dispose' method.</param>
        /// <example>
        /// The following example shows the usage of the
        /// <see cref="SuppressDisposableTransientVerificationWhenDisposeIsNotOverriddenFrom" /> method:
        /// <code lang="cs"><![CDATA[
        /// var container = new Container();
        ///
        /// container.Options
        ///     .SuppressDisposableTransientVerificationWhenDisposeIsNotOverriddenFrom(
        ///         typeof(Controller));
        ///
        /// container.Register<HomeController>(Lifestyle.Transient);
        /// container.Register<UserController>(Lifestyle.Transient);
        ///
        /// container.Verify();
        /// ]]></code>
        /// In this example, <c>HomeController</c> and <c>UserController</c> derive from <c>Controller</c>,
        /// which is defined as follows:
        /// <code lang="cs"><![CDATA[
        /// public abstract class Controller : IDisposable
        /// {
        ///     public void Dispose() => this.Dispose(true);
        ///     protected virtual void Dispose(bool disposing) { } // empty
        /// }
        /// ]]></code>
        /// Because <c>HomeController</c> and <c>UserController</c> implement <c>IDisposable</c> (by inheriting
        /// from <c>Controller</c>), a call to <see cref="Container.Verify()"/> would typically cause a
        /// diagnostic warning to be thrown, explaining that "HomeController is registered as transient, but 
        /// implements IDisposable." Because of the call to 
        /// <see cref="SuppressDisposableTransientVerificationWhenDisposeIsNotOverriddenFrom"/>, however, that
        /// warning is suppressed.
        /// </example>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="baseType"/> is a null reference.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="baseType"/> is not a class,
        /// does not implement <see cref="IDisposable"/>, or does not have a method named <i>Dispose</i> with
        /// the following signature: <c>protected virtual void Dispose(bool)</c>.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the container is disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the container is locked.</exception>
        public void SuppressDisposableTransientVerificationWhenDisposeIsNotOverriddenFrom(Type baseType)
        {
            Requires.IsNotNull(baseType, nameof(baseType));

            this.Container.ThrowWhenContainerIsLockedOrDisposed();

            if (!this.SuppressedDisposableBaseTypes.Contains(baseType))
            {
                if (!baseType.IsClass())
                {
                    throw new ArgumentException("The supplied type must be a class.", nameof(baseType));
                }

                if (!typeof(IDisposable).IsAssignableFrom(baseType))
                {
                    throw new ArgumentException(
                        "The supplied type must implement IDisposable.", nameof(baseType));
                }

                if (!Types.GetProtectedVirtualDisposeMethodsInTypeHierarchy(baseType).Any())
                {
                    throw new ArgumentException(
                        "The supplied type must have a method defined with the signature " +
                        "'protected virtual void Dispose(bool)'.",
                        nameof(baseType));
                }

                this.SuppressedDisposableBaseTypes.Add(baseType);
            }
        }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            var descriptions = new List<string>(capacity: 1);

            if (this.AllowOverridingRegistrations)
            {
                descriptions.Add("Allows Overriding Registrations");
            }

            if (!(this.ConstructorResolutionBehavior is DefaultConstructorResolutionBehavior))
            {
                descriptions.Add("Custom Constructor Resolution");
            }

            if (!(this.DependencyInjectionBehavior is DefaultDependencyInjectionBehavior))
            {
                descriptions.Add("Custom Dependency Injection");
            }

            if (!(this.PropertySelectionBehavior is DefaultPropertySelectionBehavior))
            {
                descriptions.Add("Custom Property Selection");
            }

            if (!(this.LifestyleSelectionBehavior is DefaultLifestyleSelectionBehavior))
            {
                descriptions.Add("Custom Lifestyle Selection");
            }

            if (descriptions.Count == 0)
            {
                descriptions.Add("Default Configuration");
            }

            return string.Join(", ", descriptions);
        }

        internal bool IsConstructableType(Type implementationType, out string? errorMessage)
        {
            if (!Types.IsConcreteType(implementationType))
            {
                errorMessage = StringResources.TypeShouldBeConcreteToBeUsedOnThisMethod(implementationType);
                return false;
            }

            errorMessage = null;

            try
            {
                ConstructorInfo constructor = this.SelectConstructor(implementationType);

                this.DependencyInjectionBehavior.Verify(constructor);
            }
            catch (ActivationException ex)
            {
                errorMessage = ex.Message;
            }

            return errorMessage == null;
        }

        internal ConstructorInfo SelectConstructor(Type implementationType)
        {
            var constructor = this.ConstructorResolutionBehavior.GetConstructor(implementationType);

            if (constructor == null)
            {
                throw new ActivationException(StringResources.ConstructorResolutionBehaviorReturnedNull(
                    this.ConstructorResolutionBehavior, implementationType));
            }

            return constructor;
        }

        internal InstanceProducer GetInstanceProducerFor(InjectionConsumerInfo consumer)
        {
            var producer = this.DependencyInjectionBehavior.GetInstanceProducer(consumer, throwOnFailure: true);

            // Producer will only be null if a user created a custom IConstructorInjectionBehavior that
            // returned null.
            if (producer == null)
            {
                throw new ActivationException(StringResources.DependencyInjectionBehaviorReturnedNull(
                    this.DependencyInjectionBehavior));
            }

            return producer;
        }

        internal Lifestyle SelectLifestyle(Type implementationType)
        {
            var lifestyle = this.LifestyleSelectionBehavior.SelectLifestyle(implementationType);

            if (lifestyle == null)
            {
                throw new ActivationException(StringResources.LifestyleSelectionBehaviorReturnedNull(
                    this.LifestyleSelectionBehavior, implementationType));
            }

            return lifestyle;
        }

        internal void RaiseContainerLockingAndReset()
        {
            var locking = this.containerLocking;

            if (locking != null)
            {
                // Prevent re-entry.
                this.containerLocking = null;

                locking(this.Container, new ContainerLockingEventArgs());
            }
        }

        private void ThrowWhenContainerHasRegistrations(string propertyName)
        {
            if (this.Container.IsLocked || this.Container.HasRegistrations)
            {
                throw new InvalidOperationException(
                    StringResources.PropertyCanNotBeChangedAfterTheFirstRegistration(propertyName));
            }
        }
    }
}