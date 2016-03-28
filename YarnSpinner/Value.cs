
using System;

namespace Yarn
{
	// A value from inside Yarn.
	public class Value : IComparable, IComparable<Value> {
        public static readonly Value DEFAULT = new Value();
        
		public enum Type {
            Undefined = 0, // we don't know yet or don't have a value
			Number,  // a constant number
			String,  // a string
			Bool,    // a boolean value
			Variable, // the name of a variable; will be expanded at runtime
			Null,    // the null value
		}

		public Value.Type type { get; internal set; }
        
        // this is primarily a debugging property, allowing
        // for testing between actual and perceived type
        internal object rawBacking { get; set; }

		// The underlying values for this object
		internal float numberValue {get; private set;}
		internal string variableName {get; set;}
		internal string stringValue {get; private set;}
		internal bool boolValue {get; private set;}

		public float AsNumber {
			get {
				switch (type) {
				case Type.Number:
					return numberValue;
				case Type.String:						
					try {
						return float.Parse (stringValue);
					}  catch (FormatException) {
						return 0.0f;
					}
				case Type.Bool:
					return boolValue ? 1.0f : 0.0f;
				case Type.Null:
                case Type.Undefined:
					return 0.0f;
				default:
					throw new InvalidOperationException ("Cannot cast to number from " + type.ToString());
				}
			}
		}

		public bool AsBool {
			get {
				switch (type) {
				case Type.Number:
					return !float.IsNaN(numberValue) && numberValue != 0.0f;
				case Type.String:
					return !String.IsNullOrEmpty(stringValue);
				case Type.Bool:
					return boolValue;
                case Type.Undefined:
				case Type.Null:
					return false;
				default:
					throw new InvalidOperationException ("Cannot cast to bool from " + type.ToString());
				}
			}
		}

		public string AsString {
			get {
				switch (type) {
                case Type.Undefined:
                    return "undefined";
				case Type.Number:
                    if (float.IsNaN(numberValue) ) {
                        return "NaN";
                    }
					return numberValue.ToString ();
				case Type.String:
					return stringValue;
				case Type.Bool:
					return boolValue.ToString ();
				case Type.Null:
					return "null";
				default:
					throw new ArgumentOutOfRangeException ();
				}
			}
		}

		// Create a null value
		public Value ()
		{
			this.Clear();
		}

		// Create a value with a C# object
		public Value (object value)
		{
			this.Set(value);
		}
        
        public void Set(object value) {
            this.Clear();
            
            this.rawBacking = value;
            
            // Copy an existing value
			if (typeof(Value).IsInstanceOfType(value)) {
				var otherValue = value as Value;
				type = otherValue.type;
				switch (type) {
				case Type.Number:
					numberValue = otherValue.numberValue;
					break;
				case Type.String:
					stringValue = otherValue.stringValue;
					break;
				case Type.Bool:
					boolValue = otherValue.boolValue;
					break;
				case Type.Variable:
					variableName = otherValue.variableName;
					break;
				case Type.Null:
					break;
				default:
					throw new ArgumentOutOfRangeException ();
				}
				return;
			}
			if (value == null) {
				type = Type.Null;
				return;
			}
			if (value.GetType() == typeof(string) ) {
				type = Type.String;
				stringValue = (string)value;
				return;
			}
			if (value.GetType() == typeof(int) ||
				value.GetType() == typeof(float) ||
				value.GetType() == typeof(double)) {
				type = Type.Number;
				numberValue = (float)value;
				return;
			}
			if (value.GetType() == typeof(bool) ) {
				type = Type.Bool;
				boolValue = (bool)value;
				return;
			}
			var error = string.Format("Attempted to create a Value using a {0}; currently, " +
				"Values can only be numbers, strings, bools or null.", value.GetType().Name);
			throw new YarnException(error);
        }
        
        public virtual int CompareTo(object obj) {
            var other = obj as Value;
            if (other == null) return 1;
            return this.CompareTo(other);
        }
        
        public virtual int CompareTo(Value other) {
            if (other == null) {
                return 1;
            }
            
            if (other.rawBacking == null && this.rawBacking == null) {
                return 0;
            }
            
            if (other.type == this.type) {
                return this.AsNumber.CompareTo(other.AsNumber); 
            }
            
            throw new ArgumentException(
                string.Format("Can't compare types {0} and {1}", this.type, other.type)
            );
        }
        
        public override bool Equals (object obj)
        {
            if (obj == null || GetType() != obj.GetType()) {
                return false;
            }
            
            var other = (Value)obj;
            if (this.type == other.type) {
                if (this.rawBacking == null) {
                    return other.rawBacking == null;
                }
                return this.rawBacking.Equals(other.rawBacking);
            }
            
            return base.Equals (obj);
        }
        
        // override object.GetHashCode
        public override int GetHashCode()
        {
            // TODO: yeah hay maybe fix this
            if( this.rawBacking != null ) {
                return this.rawBacking.GetHashCode();
            }
            
            return 0;
        }
        
        private void Clear() {
            this.rawBacking = null;
            this.numberValue = 0;
            this.variableName = null;
            this.stringValue = null;
            this.boolValue = false;
            this.type = Type.Undefined;
        }
        
        
        public static Value operator+ (Value a, Value b) {
            // catches:
            // undefined + string
            // number + string
            // string + string
            // bool + string
            // null + string
            if (a.type == Type.String || b.type == Type.String ) {
                // we're headed for string town!
                return new Value( a.stringValue + b.stringValue );
            }
            
            // catches:
            // undefined + number
            // undefined + null
            // (always return NaN)
            if ((a.type == Type.Undefined && (b.type == Type.Null || b.type == Type.Number)) ||
                (b.type == Type.Undefined && (a.type == Type.Null || a.type == Type.Number))
            ) {
                return new Value( float.NaN );
            }
            
            // catches:
            // number + number
            // bool (=> 0 or 1) + number
            // null (=> 0) + number 
            // bool (=> 0 or 1) + bool (=> 0 or 1)
            // null (=> 0) + null (=> 0)
            if ((a.type == Type.Number || b.type == Type.Number) || 
                (a.type == Type.Bool && b.type == Type.Bool) ||
                (a.type == Type.Null && b.type == Type.Null)
            ) {
                return new Value( a.numberValue + b.numberValue );
            }
            
            throw new System.ArgumentException( 
                string.Format("Cannot add types {0} and {1}.", a.type, b.type )    
            );
        }
        
        public static Value operator- (Value a, Value b) {
            if (a.type == Type.Number && (b.type == Type.Number || b.type == Type.Undefined || b.type == Type.Null) ||
                b.type == Type.Number && (a.type == Type.Number || a.type == Type.Undefined || a.type == Type.Null)
            ) {
                return new Value( a.numberValue - b.numberValue );
            }
            
            throw new System.ArgumentException( 
                string.Format("Cannot subtract types {0} and {1}.", a.type, b.type )    
            );
        }
        
        public static Value operator* (Value a, Value b) {
            if (a.type == Type.Number && (b.type == Type.Number || b.type == Type.Undefined || b.type == Type.Null) ||
                b.type == Type.Number && (a.type == Type.Number || a.type == Type.Undefined || a.type == Type.Null)
            ) {
                return new Value( a.numberValue * b.numberValue );
            }
            
            throw new System.ArgumentException( 
                string.Format("Cannot multiply types {0} and {1}.", a.type, b.type )    
            );
        }
        
        public static Value operator/ (Value a, Value b) {
            if (a.type == Type.Number && (b.type == Type.Number || b.type == Type.Undefined || b.type == Type.Null) ||
                b.type == Type.Number && (a.type == Type.Number || a.type == Type.Undefined || a.type == Type.Null)
            ) {
                return new Value( a.numberValue / b.numberValue );
            }
            
            throw new System.ArgumentException( 
                string.Format("Cannot divide types {0} and {1}.", a.type, b.type )    
            );
        }
        
        public static Value operator- (Value a) {
            if( a.type == Type.Number ) {
                return new Value( -a.AsNumber );
            }
            if (a.type == Type.String &&
               (a.AsString == null || a.AsString.Trim() == "") 
            ) {
                return new Value( -0 );
            }
            return new Value( float.NaN );
        }
        
        // Define the is greater than operator.
        public static bool operator >  (Value operand1, Value operand2)
        {
            return operand1.CompareTo(operand2) == 1;
        }

        // Define the is less than operator.
        public static bool operator <  (Value operand1, Value operand2)
        {
            return operand1.CompareTo(operand2) == -1;
        }

        // Define the is greater than or equal to operator.
        public static bool operator >=  (Value operand1, Value operand2)
        {
            return operand1.CompareTo(operand2) >= 0;
        }

        // Define the is less than or equal to operator.
        public static bool operator <=  (Value operand1, Value operand2)
        {
            return operand1.CompareTo(operand2) <= 0;
        }
	}	
}
