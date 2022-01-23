﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;

namespace EntityObserver
{
    /// <summary>
    /// A base implementation for the <see cref="INotifyPropertyChanged"/> and <see cref="INotifyDataErrorInfo"/>.
    /// </summary>
    public abstract class ValidationNotifier : INotifyPropertyChanged, INotifyDataErrorInfo, IValidator
    {
        private readonly Dictionary<string, List<string?>> _errors;

        /// <summary>
        /// Creates a new instance of a <see cref="ValidationNotifier"/> base class.
        /// </summary>
        protected ValidationNotifier()
        {
            _errors = new Dictionary<string, List<string?>>();
        }

        /// <inheritdoc />
        public virtual bool HasErrors => _errors.Any();

        /// <inheritdoc />
        public virtual bool IsValid => !HasErrors;

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <inheritdoc />
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        /// <inheritdoc />
        public IEnumerable GetErrors(string? propertyName)
        {
            return propertyName is not null && _errors.ContainsKey(propertyName)
                ? _errors[propertyName]!
                : Enumerable.Empty<string>();
        }

        /// <inheritdoc />
        public void Validate(bool requiredOnly = false)
        {
            if (requiredOnly)
            {
                ValidateRequired();
                return;
            }
            
            ValidateObject();
        }

        /// <inheritdoc />
        public void Validate(string propertyName, object? value) => ValidateProperty(propertyName, value);

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for the name of the provided property.
        /// </summary>
        /// <param name="propertyName">
        /// Name of the property to raise the value change notification for.
        /// This value is optional and can be provided automatically when invoked from compilers
        /// that support <see cref="CallerMemberNameAttribute"/>.
        /// </param>
        protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Raises the <see cref="ErrorsChanged"/> event for the name of the provided property.
        /// </summary>
        /// <param name="propertyName">
        /// Name of the property to raise the error change notification for.
        /// This value is optional and can be provided automatically when invoked from compilers
        /// that support <see cref="CallerMemberNameAttribute"/>.
        /// </param>
        private void RaiseErrorsChanged([CallerMemberName] string? propertyName = null)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Performs validation on the entire object using the <see cref="Validator"/> class.
        /// </summary>
        private void ValidateObject()
        {
            ClearErrors();

            var results = new List<ValidationResult>();
            var context = new ValidationContext(this);
            Validator.TryValidateObject(this, context, results, true);

            UpdateErrors(results);
        }

        /// <summary>
        /// Performs validation on all <see cref="RequiredAttribute"/> properties using the <see cref="Validator"/> class.
        /// </summary>
        private void ValidateRequired()
        {
            ClearRequired();

            var results = new List<ValidationResult>();
            var context = new ValidationContext(this);
            Validator.TryValidateObject(this, context, results, false);

            UpdateErrors(results);
        }

        /// <summary>
        /// Performs validation on the specified property with the provided new property value.
        /// </summary>
        private void ValidateProperty(string propertyName, object? value)
        {
            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentException("Property name can not be null or empty for property validation");

            ClearError(propertyName);

            var results = new List<ValidationResult>();
            var context = new ValidationContext(this) { MemberName = propertyName };
            Validator.TryValidateProperty(value, context, results);

            UpdateErrors(results);
        }

        /// <summary>
        /// Updates the errors collection with the validation results.
        /// </summary>
        /// <param name="results">The collection of validation results to process.</param>
        private void UpdateErrors(IReadOnlyCollection<ValidationResult> results)
        {
            if (!results.Any()) return;

            var propertyNames = results.SelectMany(r => r.MemberNames).Distinct().ToList();

            foreach (var propertyName in propertyNames)
            {
                var errors = results.Where(r => r.MemberNames.Contains(propertyName))
                    .Select(r => r.ErrorMessage).Distinct().ToList();

                _errors[propertyName] = errors;

                RaiseErrorsChanged(propertyName);
            }

            RaisePropertyChanged(nameof(HasErrors));
        }

        /// <summary>
        /// Clears all validation errors from the errors collection of the current object.
        /// </summary>
        private void ClearErrors()
        {
            if (!HasErrors) return;
            
            foreach (var propertyName in _errors.Keys.ToList())
            {
                _errors.Remove(propertyName);
                RaiseErrorsChanged(propertyName);
            }
            
            RaisePropertyChanged(nameof(HasErrors));
        }

        private void ClearRequired()
        {
            foreach (var propertyName in _errors.Keys.ToList())
            {
                var attribute = GetType().FindAttribute<RequiredAttribute>(propertyName);
                if (attribute is null) continue;

                var error = _errors[propertyName].FirstOrDefault(e => e == attribute.ErrorMessage);
                if (error is null) continue;

                _errors.Remove(error);
                RaiseErrorsChanged(propertyName);
            }
        }


        /// <summary>
        /// Clears validation errors for the specified property from the errors collection of the current object.
        /// </summary>
        private void ClearError(string propertyName)
        {
            if (!_errors.ContainsKey(propertyName)) return;
            
            _errors.Remove(propertyName);
            RaiseErrorsChanged(propertyName);
            RaisePropertyChanged(nameof(HasErrors));
        }
    }
}