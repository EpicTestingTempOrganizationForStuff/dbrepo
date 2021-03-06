using System;
using System.Reflection;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Diagnostics;
using System.Collections.Generic;
using DbRepo.Exceptions;
using DbRepo.Attributes;

namespace DbRepo
{
	public class DbRepo<T> where T : class
	{
		private readonly DbSet<T> _set;
		private readonly DbContext _db;
		private Dictionary<string, string> _propertyMap;
		/// <summary>
		/// Creates a new database repository
		/// </summary>
		/// <example>
		/// <code>
		/// DbRepo<User> = new DbRepo<User>(Users, this);
		/// </code>
		/// </example>
		/// <param name="set">The DbSet for the entity to make the repository for</param>
		/// <param name="db">The DbContext</param>
		/// <exception cref="RepoInstantiationException"></exception>
		public DbRepo(DbSet<T> set, DbContext db) {
			try
			{
				this._set = set;
				this._db = db;
				_propertyMap = new Dictionary<string, string>();
				foreach (PropertyInfo property in typeof(T).GetProperties())
				{
					//Check if attribute of type RepoColumnName
					RepoColumnName? attribute = property.GetCustomAttribute<RepoColumnName>(true);
					if (attribute == null)
						continue; //Continue to next property if not
					string propName = property.Name;
					string actualName = attribute.Name;
					_propertyMap.Add(actualName, propName); //Map the custom column name to actual property name
					_propertyMap.Add(propName, propName); //Map the normal property name to normal property name for validation
				}
			}
			catch(Exception ex)
			{
				throw new RepoInstantiationException(ex);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		/// <exception cref="ExpressionBuilderException"></exception>
		private Expression<Func<T, bool>>BuildExpression(object obj, bool skipValidation = false)
		{
			try
			{
				//Convert object to the Type we have received using extension method
				T convertedObj = obj.ToType<T>();
				//This is the initial input, we will name the input Object so this will essentially be
				//Object =>
				ParameterExpression input = Expression.Parameter(convertedObj.GetType(), "Object");
				//Init as null, this will allow us to check whether to add an AND or to instantiate further on in the loop
				BinaryExpression? finalExpression = null;

				//Get a list of properties of the object, so that we can get key, value
				PropertyInfo[] properties = obj.GetType().GetProperties();
				foreach (PropertyInfo prop in properties) //Loop through
				{
					//Get the value from the object, we are not using the converted object as default values
					//for fields create issues, so if there was an empty field it may end up being added as an AND
					object? obj2 = prop.GetValue(obj);
					//If null continue with loop
					if (obj2 == null) continue;

					string actualName;
					if (!_propertyMap.TryGetValue(prop.Name, out actualName))
					{
						if (skipValidation)
							continue;
						throw new ExpressionBuilderException($"{prop.Name} is not a valid property of {nameof(T)}");
					}
						
					//Get property name from Object => Declared above, will essentially be Object.PropName
					MemberExpression property = Expression.Property(input, actualName);
					//Create a constant value using the property value
					Expression comparison = Expression.Constant(obj2);
					//Compare with the property of the object with the constant to ensure that they are equal
					BinaryExpression result = Expression.Equal(property, comparison);

					// Set result to final expression or append it to the existing expression
					finalExpression = finalExpression != null ? Expression.And(finalExpression, result) : result;
				}

				//Return the created lambda function
				return Expression.Lambda<Func<T, bool>>(finalExpression, input);
			}
			catch (Exception ex)
			{
				throw new ExpressionBuilderException(ex);
			}
		}

		#region Non-Asynchronous

		/// <summary>
		/// 
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public T FindOne(object obj, bool skipValidation = false)
		{
			Expression<Func<T, bool>>? expression = BuildExpression(obj, skipValidation);
			return _set.FirstOrDefault(expression);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public T FindOneAndForget(object obj, bool skipValidation = false)
		{
			Expression<Func<T, bool>>? expression = BuildExpression(obj, skipValidation);
			return _set.AsNoTracking().FirstOrDefault(expression);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public T InsertOne(T obj)
		{
			_set.Add(obj);
			int result = _db.SaveChanges();
			return obj;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="objArr"></param>
		/// <returns></returns>
		public IEnumerable<T> InsertMany(IEnumerable<T> objArr)
		{
			_set.AddRange(objArr);
			int result = _db.SaveChanges();
			return objArr;
		}
		#endregion

		#region Asynchronous
		/// <summary>
		/// 
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public async Task<T> FindOneAsync(object obj, bool skipValidation = false) {
			Expression<Func<T, bool>>? expression = BuildExpression(obj, skipValidation);
			return await _set.FirstOrDefaultAsync(expression).ConfigureAwait(false);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public async Task<T> FindOneAndForgetAsync(object obj, bool skipValidation = false) {
			Expression<Func<T, bool>> expression = BuildExpression(obj, skipValidation);
			return await _set.AsNoTracking().FirstOrDefaultAsync(expression).ConfigureAwait(false);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public async Task<T> InsertOneAsync(T obj) {
			await _set.AddAsync(obj).ConfigureAwait(false);
			int result = await _db.SaveChangesAsync().ConfigureAwait(false);
			return obj;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="objArr"></param>
		/// <returns></returns>
		public async Task<IEnumerable<T>> InsertManyAsync(IEnumerable<T> objArr)
		{
			await _set.AddRangeAsync(objArr).ConfigureAwait(false);
			int result = await _db.SaveChangesAsync().ConfigureAwait(false);
			return objArr;
		}
		#endregion
	}
}